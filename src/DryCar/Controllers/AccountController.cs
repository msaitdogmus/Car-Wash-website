using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using BCrypt.Net;
using DryCar.Data;
using DryCar.Models;
using DryCar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DryCar.Controllers;

public class AccountController : Controller
{
    public class FaceLoginModel
    {
        public string FaceBase64 { get; set; }

        public List<string> FramesBase64 { get; set; }
    }

    private readonly ApplicationDbContext _context;

    private readonly IEmailSender _emailSender;

    private readonly IConfiguration _config;

    private readonly IFaceVectorProtector _faceVectorProtector;

    public AccountController(
        ApplicationDbContext context,
        IEmailSender emailSender,
        IConfiguration config,
        IFaceVectorProtector faceVectorProtector)
    {
        _context = context;
        _emailSender = emailSender;
        _config = config;
        _faceVectorProtector = faceVectorProtector;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public IActionResult Login(string email, string password)
    {
        string normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        User user = _context.Users.FirstOrDefault((User u) => u.Email == normalizedEmail);
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            if (string.IsNullOrEmpty(user.FaceVector))
            {
                base.ViewBag.Error = "Bu hesapta yüz kaydı bulunamadı. Lütfen yöneticiyle iletişime geçin veya yeniden kayıt olun.";
                return View();
            }
            base.HttpContext.Session.Clear();
            base.HttpContext.Session.SetInt32("PendingUserId", user.Id);
            return RedirectToAction("VerifyFace");
        }
        base.ViewBag.Error = "Geçersiz kullanıcı adı veya şifre";
        return View();
    }

    [HttpGet]
    public IActionResult VerifyFace()
    {
        if (!base.HttpContext.Session.GetInt32("PendingUserId").HasValue)
        {
            return RedirectToAction("Login");
        }
        return View();
    }

    [HttpPost]
    [RequestSizeLimit(16_000_000)]
    [EnableRateLimiting("face")]
    public IActionResult VerifyFacePost([FromBody] FaceLoginModel model)
    {
        Dictionary<string, object> diag = new Dictionary<string, object>();
        List<string> framePaths = new List<string>();
        try
        {
            diag["stage"] = "start";
            diag["serverTimeUtc"] = DateTime.UtcNow.ToString("O");
            diag["contentType"] = base.Request?.ContentType ?? "";
            diag["contentLength"] = (base.Request?.ContentLength).GetValueOrDefault();
            int? pendingUserId = base.HttpContext.Session.GetInt32("PendingUserId");
            diag["pendingUserId"] = pendingUserId;
            diag["sessionPendingUserExists"] = pendingUserId.HasValue;
            if (!pendingUserId.HasValue)
            {
                diag["stage"] = "session_check_failed";
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "session_check_failed",
                    message = "Oturum zaman aşımı. Tekrar giriş yapın.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            User user = _context.Users.Find(pendingUserId.Value);
            diag["userFound"] = user != null;
            if (user == null)
            {
                diag["stage"] = "user_not_found";
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "user_not_found",
                    message = "Kullanıcı bulunamadı.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            diag["userId"] = user.Id;
            diag["hasStoredFaceVector"] = !string.IsNullOrWhiteSpace(user.FaceVector);
            diag["storedFaceVectorLength"] = user.FaceVector?.Length ?? 0;
            diag["modelIsNull"] = model == null;
            diag["faceBase64Exists"] = !string.IsNullOrWhiteSpace(model?.FaceBase64);
            diag["faceBase64Length"] = (model?.FaceBase64?.Length).GetValueOrDefault();
            diag["framesExists"] = model?.FramesBase64 != null;
            diag["framesCount"] = (model?.FramesBase64?.Count).GetValueOrDefault();
            if (model == null)
            {
                diag["stage"] = "request_model_null";
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "request_model_null",
                    message = "İstek gövdesi boş geldi.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            if (model.FramesBase64 == null || model.FramesBase64.Count < 6)
            {
                diag["stage"] = "insufficient_frames";
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "insufficient_frames",
                    message = "Canlılık doğrulaması için yeterli görüntü alınamadı. Tekrar deneyin.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            string tempDir = (string)(diag["tempDir"] = GetFaceTempDirectory());
            diag["tempDirExistsBefore"] = Directory.Exists(tempDir);
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                diag["tempDirExistsAfter"] = Directory.Exists(tempDir);
            }
            catch (Exception ex)
            {
                diag["stage"] = "temp_dir_create_failed";
                diag["tempDirCreateError"] = ex.ToString();
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "temp_dir_create_failed",
                    message = "Geçici klasör oluşturulamadı.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            int totalFrames = model.FramesBase64.Count;
            int nullOrWhiteFrames = 0;
            int invalidBase64Frames = 0;
            int writtenFrames = 0;
            long totalWrittenBytes = 0L;
            List<object> writtenFiles = new List<object>();
            for (int i = 0; i < model.FramesBase64.Count; i++)
            {
                string frame = model.FramesBase64[i];
                if (string.IsNullOrWhiteSpace(frame))
                {
                    nullOrWhiteFrames++;
                    continue;
                }
                string base64;
                try
                {
                    base64 = (frame.Contains(",") ? frame.Split(',')[1] : frame);
                }
                catch (Exception ex2)
                {
                    invalidBase64Frames++;
                    writtenFiles.Add(new
                    {
                        index = i,
                        ok = false,
                        error = "base64_split_failed",
                        detail = ex2.Message
                    });
                    continue;
                }
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(base64);
                }
                catch (Exception ex3)
                {
                    invalidBase64Frames++;
                    writtenFiles.Add(new
                    {
                        index = i,
                        ok = false,
                        error = "base64_decode_failed",
                        detail = ex3.Message,
                        inputLength = (base64?.Length ?? 0)
                    });
                    continue;
                }
                if (bytes.Length == 0 || bytes.Length > 2_000_000 || totalWrittenBytes + bytes.Length > 12_000_000)
                {
                    invalidBase64Frames++;
                    writtenFiles.Add(new
                    {
                        index = i,
                        ok = false,
                        error = "frame_size_limit"
                    });
                    continue;
                }
                string filePath = Path.Combine(tempDir, $"temp_blink_{user.Id}_{Guid.NewGuid():N}_{i}.jpg");
                try
                {
                    System.IO.File.WriteAllBytes(filePath, bytes);
                    bool exists = System.IO.File.Exists(filePath);
                    long size = (exists ? new FileInfo(filePath).Length : 0);
                    if (exists && size > 0)
                    {
                        framePaths.Add(filePath);
                        writtenFrames++;
                        totalWrittenBytes += size;
                    }
                    writtenFiles.Add(new
                    {
                        index = i,
                        ok = (exists && size > 0),
                        filePath = filePath,
                        exists = exists,
                        sizeBytes = size
                    });
                }
                catch (Exception ex4)
                {
                    writtenFiles.Add(new
                    {
                        index = i,
                        ok = false,
                        filePath = filePath,
                        error = "file_write_failed",
                        detail = ex4.ToString()
                    });
                }
            }
            diag["stage"] = "frames_processed";
            diag["totalFrames"] = totalFrames;
            diag["nullOrWhiteFrames"] = nullOrWhiteFrames;
            diag["invalidBase64Frames"] = invalidBase64Frames;
            diag["writtenFrames"] = writtenFrames;
            diag["totalWrittenBytes"] = totalWrittenBytes;
            diag["writtenFiles"] = writtenFiles;
            diag["framePathsCount"] = framePaths.Count;
            if (framePaths.Count < 6)
            {
                diag["stage"] = "not_enough_written_files";
                SaveFaceDebug(diag);
                return Json(new
                {
                    success = false,
                    stage = "not_enough_written_files",
                    message = "Görüntü dosyaları yeterli sayıda üretilemedi.",
                    diagnostics = PublicDiagnostics(diag)
                });
            }
            diag["pythonEnabled"] = (_config["PythonConfig:Enabled"] ?? "true").ToLower() == "true";
            diag["pythonExecutableResolved"] = GetPythonExecutable();
            diag["pythonScriptResolved"] = GetPythonScriptPath();
            diag["pythonExecutableExists"] = IsProbablyExistingFile(diag["pythonExecutableResolved"]?.ToString());
            diag["pythonScriptExists"] = System.IO.File.Exists(diag["pythonScriptResolved"]?.ToString() ?? "");
            diag["pythonModelType"] = _config["PythonConfig:ModelType"] ?? "hog";
            diag["pythonMinFaceArea"] = _config["PythonConfig:MinFaceArea"] ?? "6400";
            diag["pythonMinSharpness"] = _config["PythonConfig:MinSharpness"] ?? "25";
            diag["pythonDebugMode"] = _config["PythonConfig:DebugMode"] ?? "false";
            diag["stage"] = "python_before_run";
            string pythonResult = RunPythonWithBlink(framePaths);
            diag["stage"] = "python_after_run";
            diag["pythonResultKind"] = IsPythonStatus(pythonResult) ? pythonResult : "VECTOR";
            diag["pythonResultLength"] = pythonResult?.Length ?? 0;
            switch (pythonResult)
            {
                case "PYTHON_TIMEOUT":
                    SaveFaceDebug(diag);
                    return Json(new
                    {
                        success = false,
                        stage = "python_timeout",
                        message = "Gözünü hiç kırpmadın ya da çok kırptın.. Tekrar dene.",
                        diagnostics = PublicDiagnostics(diag)
                    });
                case "PYTHON_ERROR":
                    SaveFaceDebug(diag);
                    return Json(new
                    {
                        success = false,
                        stage = "python_error",
                        message = "tekrar dene.",
                        diagnostics = PublicDiagnostics(diag)
                    });
                case "NO_BLINK":
                    SaveFaceDebug(diag);
                    return Json(new
                    {
                        success = false,
                        stage = "python_no_blink",
                        message = "Canlılık doğrulanamadı. Lütfen doğru anda 1 kez göz kırpıp tekrar deneyin.",
                        diagnostics = PublicDiagnostics(diag)
                    });
                default:
                    if (!string.IsNullOrWhiteSpace(pythonResult))
                    {
                        string savedVector = _faceVectorProtector.Unprotect(user.FaceVector);
                        bool compareOk = CompareVectors(savedVector, pythonResult);
                        diag["stage"] = "vector_compare_done";
                        diag["vectorCompareOk"] = compareOk;
                        diag["storedVectorLength"] = user.FaceVector?.Length ?? 0;
                        diag["currentVectorLength"] = pythonResult?.Length ?? 0;
                        if (compareOk)
                        {
                            SetSession(user);
                            base.HttpContext.Session.Remove("PendingUserId");
                            SaveFaceDebug(diag);
                            return Json(new
                            {
                                success = true,
                                stage = "success",
                                message = "Doğrulama Başarılı! Yönlendiriliyorsunuz...",
                                diagnostics = PublicDiagnostics(diag)
                            });
                        }
                        SaveFaceDebug(diag);
                        return Json(new
                        {
                            success = false,
                            stage = "vector_mismatch",
                            message = "Yüz eşleşmedi! Bu kişi siz değilsiniz.",
                            diagnostics = PublicDiagnostics(diag)
                        });
                    }
                    goto case "NO_FACE";
                case "NO_FACE":
                    SaveFaceDebug(diag);
                    return Json(new
                    {
                        success = false,
                        stage = "python_no_face",
                        message = "Yüz algılanamadı.. tekrar dene",
                        diagnostics = PublicDiagnostics(diag)
                    });
            }
        }
        catch (Exception ex5)
        {
            diag["stage"] = "unhandled_exception";
            diag["exception"] = ex5.ToString();
            SaveFaceDebug(diag);
            return Json(new
            {
                success = false,
                stage = "unhandled_exception",
                message = "Beklenmeyen bir hata oluştu.",
                diagnostics = PublicDiagnostics(diag)
            });
        }
        finally
        {
            foreach (string fp in framePaths)
            {
                try
                {
                    if (System.IO.File.Exists(fp))
                    {
                        System.IO.File.Delete(fp);
                    }
                }
                catch
                {
                }
            }
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        Dictionary<string, object> diag = new Dictionary<string, object>();
        string? tempFile = null;
        try
        {
            diag["stage"] = "register_start";
            diag["faceBase64Exists"] = !string.IsNullOrWhiteSpace(model.FaceBase64);
            diag["faceBase64Length"] = model.FaceBase64?.Length ?? 0;
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (!model.FaceBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                diag["stage"] = "register_facebase64_invalid";
                SaveFaceDebug(diag);
                base.ViewBag.Error = "Kayıt olabilmek için yüz tanımlama zorunludur.";
                return View(model);
            }
            int separator = model.FaceBase64.IndexOf(',');
            string base64Clean = separator >= 0 ? model.FaceBase64[(separator + 1)..] : model.FaceBase64;
            byte[] bytes = Convert.FromBase64String(base64Clean);
            if (bytes.Length == 0 || bytes.Length > 5_000_000)
            {
                base.ViewBag.Error = "Yüz görüntüsü izin verilen boyutu aşıyor.";
                return View(model);
            }
            string faceDir = GetFaceTempDirectory();
            Directory.CreateDirectory(faceDir);
            tempFile = Path.Combine(faceDir, $"temp_register_{Guid.NewGuid():N}.jpg");
            await System.IO.File.WriteAllBytesAsync(tempFile, bytes);
            diag["tempFile"] = tempFile;
            diag["tempFileExists"] = System.IO.File.Exists(tempFile);
            diag["tempFileSize"] = (System.IO.File.Exists(tempFile) ? new FileInfo(tempFile).Length : 0);
            string vector = RunPython(tempFile);
            diag["pythonResultKind"] = IsPythonStatus(vector) ? vector : "VECTOR";
            diag["pythonResultLength"] = vector?.Length ?? 0;
            if (vector == "PYTHON_ERROR")
            {
                diag["stage"] = "register_python_error";
                SaveFaceDebug(diag);
                base.ViewBag.Error = "Python çalıştırılırken hata oluştu.";
                return View(model);
            }
            if (vector == "NO_FACE" || string.IsNullOrWhiteSpace(vector))
            {
                diag["stage"] = "register_no_face";
                SaveFaceDebug(diag);
                base.ViewBag.Error = "Yüz algılanamadı. Lütfen kameraya net bakıp tekrar deneyin.";
                return View(model);
            }
            User user = new User
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Phone = model.Phone.Trim(),
                Email = model.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 12),
                FaceVector = _faceVectorProtector.Protect(vector),
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            diag["stage"] = "register_success";
            diag["createdUserId"] = user.Id;
            SaveFaceDebug(diag);
            base.TempData["SuccessMessage"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
            return RedirectToAction("Login", "Account");
        }
        catch (FormatException)
        {
            base.ViewBag.Error = "Yüz görüntüsü okunamadı. Lütfen yeniden çekin.";
            return View(model);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
        {
            base.ViewBag.Error = "Bu e-posta adresi zaten kayıtlı.";
            return View(model);
        }
        catch (Exception ex2)
        {
            diag["stage"] = "register_exception";
            diag["exception"] = ex2.ToString();
            SaveFaceDebug(diag);
            base.ViewBag.Error = "Kayıt sırasında hata oluştu.";
            return View(model);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFile))
            {
                try
                {
                    System.IO.File.Delete(tempFile);
                }
                catch
                {
                    // Geçici dosya temizliği ana kayıt yanıtını bozmamalı.
                }
            }
        }
    }

    private void SetSession(User user)
    {
        base.HttpContext.Session.SetInt32("UserId", user.Id);
        base.HttpContext.Session.SetString("UserName", user.FirstName);
    }

    private string GetFaceTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "drycar-face");
    }

    private bool IsProbablyExistingFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        try
        {
            if (Path.IsPathRooted(path))
            {
                return System.IO.File.Exists(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveFaceDebug(object obj)
    {
        if (!string.Equals(_config["PythonConfig:DebugMode"], "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            string text = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            base.HttpContext.Session.SetString("LastFaceDebug", text);
        }
        catch
        {
        }
    }

    private string GetPythonExecutable()
    {
        try
        {
            string configValue = _config["PythonConfig:ExecutablePath"];
            if (string.IsNullOrWhiteSpace(configValue))
            {
                throw new FileNotFoundException("PythonConfig:ExecutablePath boş.");
            }
            if (!Path.IsPathRooted(configValue))
            {
                return configValue;
            }
            if (!System.IO.File.Exists(configValue))
            {
                throw new FileNotFoundException("Configured python.exe bulunamadı: " + configValue);
            }
            return configValue;
        }
        catch (Exception ex)
        {
            base.HttpContext.Session.SetString("LastFaceDebug", "GetPythonExecutable error: " + ex);
            throw;
        }
    }

    private string GetPythonScriptPath()
    {
        try
        {
            List<string> candidates = new List<string>();
            string configValue = _config["PythonConfig:ScriptPath"];
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                if (Path.IsPathRooted(configValue))
                {
                    candidates.Add(configValue);
                }
                else
                {
                    candidates.Add(Path.Combine(AppContext.BaseDirectory, configValue));
                }
            }
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "python", "extract_vector.py"));
            foreach (string candidate in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
            throw new FileNotFoundException("Geçerli bir extract_vector.py yolu bulunamadı.");
        }
        catch (Exception ex)
        {
            base.HttpContext.Session.SetString("LastFaceDebug", "GetPythonScriptPath error: " + ex);
            throw;
        }
    }

    private string RunPython(string imagePath)
    {
        try
        {
            string pythonExe = GetPythonExecutable();
            string scriptPath = GetPythonScriptPath();
            string modelType = _config["PythonConfig:ModelType"] ?? "hog";
            string minFaceArea = _config["PythonConfig:MinFaceArea"] ?? "6400";
            string minSharpness = _config["PythonConfig:MinSharpness"] ?? "25";
            bool debugMode = (_config["PythonConfig:DebugMode"] ?? "false").ToLower() == "true";
            string arguments = $"\"{scriptPath}\" \"{imagePath}\" --model {modelType} --min_face_area {minFaceArea} --min_sharpness {minSharpness}";
            if (debugMode)
            {
                arguments += " --debug";
            }
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            base.HttpContext.Session.SetString("LastFacePythonExe", pythonExe);
            base.HttpContext.Session.SetString("LastFaceScriptPath", scriptPath);
            using Process process = new Process();
            process.StartInfo = psi;
            if (!process.Start())
            {
                base.HttpContext.Session.SetString("LastFaceDebug", $"Process.Start false. PYTHON={pythonExe} | SCRIPT={scriptPath} | IMAGE={imagePath}");
                base.HttpContext.Session.SetInt32("LastFaceExitCode", -999);
                base.HttpContext.Session.SetString("LastFaceStdout", "");
                base.HttpContext.Session.SetString("LastFaceStderr", "");
                return "NO_FACE";
            }
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
                base.HttpContext.Session.SetInt32("LastFaceExitCode", -998);
                base.HttpContext.Session.SetString("LastFaceStdout", "");
                base.HttpContext.Session.SetString("LastFaceStderr", "PYTHON_TIMEOUT");
                base.HttpContext.Session.SetString("LastFaceDebug", $"PYTHON TIMEOUT. PYTHON={pythonExe}\nSCRIPT={scriptPath}\nIMAGE={imagePath}\nARGS={arguments}");
                return "PYTHON_TIMEOUT";
            }
            Task.WaitAll(stdoutTask, stderrTask);
            string stdout = stdoutTask.Result ?? "";
            string stderr = stderrTask.Result ?? "";
            base.HttpContext.Session.SetInt32("LastFaceExitCode", process.ExitCode);
            if (debugMode)
            {
                base.HttpContext.Session.SetString("LastFaceStdout", stdout);
                base.HttpContext.Session.SetString("LastFaceStderr", stderr);
                base.HttpContext.Session.SetString("LastFaceDebug", $"PYTHON={pythonExe}\nSCRIPT={scriptPath}\nIMAGE={imagePath}\nIMAGE_EXISTS={System.IO.File.Exists(imagePath)}\nIMAGE_SIZE={(System.IO.File.Exists(imagePath) ? new FileInfo(imagePath).Length : 0)}\nEXIT={process.ExitCode}\nSTDERR={stderr}\nSTDOUT={stdout}");
            }
            else
            {
                base.HttpContext.Session.Remove("LastFaceStdout");
                base.HttpContext.Session.Remove("LastFaceStderr");
                base.HttpContext.Session.Remove("LastFaceDebug");
            }
            string result = stdout.Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                return "NO_FACE";
            }
            return result;
        }
        catch (Exception ex)
        {
            base.HttpContext.Session.SetString("LastFaceDebug", ex.ToString());
            base.HttpContext.Session.SetInt32("LastFaceExitCode", -999);
            base.HttpContext.Session.SetString("LastFaceStdout", "");
            base.HttpContext.Session.SetString("LastFaceStderr", ex.ToString());
            return "PYTHON_ERROR";
        }
    }

    private string RunPythonWithBlink(List<string> imagePaths)
    {
        try
        {
            string pythonExe = GetPythonExecutable();
            string scriptPath = GetPythonScriptPath();
            string modelType = _config["PythonConfig:ModelType"] ?? "hog";
            string minFaceArea = _config["PythonConfig:MinFaceArea"] ?? "6400";
            string minSharpness = _config["PythonConfig:MinSharpness"] ?? "25";
            bool debugMode = (_config["PythonConfig:DebugMode"] ?? "false").ToLower() == "true";
            IEnumerable<string> quoted = imagePaths.Select((string p) => "\"" + p + "\"");
            string imagesArg = string.Join(" ", quoted);
            string arguments = $"\"{scriptPath}\" {imagesArg} --blink --model {modelType} --min_face_area {minFaceArea} --min_sharpness {minSharpness}";
            if (debugMode)
            {
                arguments += " --debug";
            }
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            base.HttpContext.Session.SetString("LastFacePythonExe", pythonExe);
            base.HttpContext.Session.SetString("LastFaceScriptPath", scriptPath);
            using Process process = new Process();
            process.StartInfo = psi;
            if (!process.Start())
            {
                base.HttpContext.Session.SetString("LastFaceDebug", $"Process.Start false. PYTHON={pythonExe} | SCRIPT={scriptPath} | IMAGES={string.Join(" | ", imagePaths)}");
                base.HttpContext.Session.SetInt32("LastFaceExitCode", -999);
                base.HttpContext.Session.SetString("LastFaceStdout", "");
                base.HttpContext.Session.SetString("LastFaceStderr", "");
                return "NO_FACE";
            }
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
                base.HttpContext.Session.SetInt32("LastFaceExitCode", -998);
                base.HttpContext.Session.SetString("LastFaceStdout", "");
                base.HttpContext.Session.SetString("LastFaceStderr", "PYTHON_TIMEOUT");
                base.HttpContext.Session.SetString("LastFaceDebug", $"PYTHON TIMEOUT. PYTHON={pythonExe}\nSCRIPT={scriptPath}\nIMAGES={string.Join(" | ", imagePaths)}\nARGS={arguments}");
                return "PYTHON_TIMEOUT";
            }
            Task.WaitAll(stdoutTask, stderrTask);
            string stdout = stdoutTask.Result ?? "";
            string stderr = stderrTask.Result ?? "";
            base.HttpContext.Session.SetInt32("LastFaceExitCode", process.ExitCode);
            if (debugMode)
            {
                base.HttpContext.Session.SetString("LastFaceStdout", stdout);
                base.HttpContext.Session.SetString("LastFaceStderr", stderr);
                base.HttpContext.Session.SetString("LastFaceDebug", $"PYTHON={pythonExe}\nSCRIPT={scriptPath}\nIMAGES={string.Join(" | ", imagePaths)}\nEXIT={process.ExitCode}\nSTDERR={stderr}\nSTDOUT={stdout}");
            }
            else
            {
                base.HttpContext.Session.Remove("LastFaceStdout");
                base.HttpContext.Session.Remove("LastFaceStderr");
                base.HttpContext.Session.Remove("LastFaceDebug");
            }
            string result = stdout.Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                return "NO_FACE";
            }
            return result;
        }
        catch (Exception ex)
        {
            base.HttpContext.Session.SetString("LastFaceDebug", ex.ToString());
            base.HttpContext.Session.SetInt32("LastFaceExitCode", -999);
            base.HttpContext.Session.SetString("LastFaceStdout", "");
            base.HttpContext.Session.SetString("LastFaceStderr", ex.ToString());
            return "PYTHON_ERROR";
        }
    }

    private bool CompareVectors(string savedVec, string currentVec)
    {
        try
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            double[] v1 = (from n in savedVec.Split(',')
                           select double.Parse(n, culture)).ToArray();
            double[] v2 = (from n in currentVec.Split(',')
                           select double.Parse(n, culture)).ToArray();
            if (v1.Length != v2.Length || v1.Length == 0)
            {
                return false;
            }
            double sum = 0.0;
            for (int i = 0; i < v1.Length; i++)
            {
                sum += Math.Pow(v1[i] - v2[i], 2.0);
            }
            return Math.Sqrt(sum) < 0.6;
        }
        catch
        {
            return false;
        }
    }

    [HttpGet]
    public IActionResult Logout()
    {
        base.HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            base.ViewBag.Error = "E-posta boş olamaz.";
            return View();
        }
        string normalizedEmail = email.Trim().ToLowerInvariant();
        User user = await _context.Users.FirstOrDefaultAsync((User u) => u.Email == normalizedEmail);
        if (user == null)
        {
            base.ViewBag.Success = "Eğer bu e-posta kayıtlıysa, sıfırlama bağlantısı gönderildi.";
            return View();
        }
        string resetToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = HashResetToken(resetToken);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30.0);
        await _context.SaveChangesAsync();
        string baseUrl = _config["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{base.Request.Scheme}://{base.Request.Host}";
        }
        string resetLink = baseUrl + "/Account/ResetPassword?token=" + Uri.EscapeDataString(resetToken);
        string safeResetLink = WebUtility.HtmlEncode(resetLink);
        string subject = "Şifre Sıfırlama";
        string body = $"\r\n        <p>Şifre sıfırlama talebinde bulundunuz.</p>\r\n        <p>Link: <a href=\"{safeResetLink}\">{safeResetLink}</a></p>\r\n        <p>Bu bağlantı 30 dakika geçerlidir.</p>";
        await _emailSender.SendEmailAsync(user.Email, subject, body);
        base.ViewBag.Success = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi.";
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return View("ResetPasswordExpired");
        }
        return View(new ResetPasswordViewModel
        {
            Token = token
        });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (vm == null || string.IsNullOrWhiteSpace(vm.Token))
        {
            return View("ResetPasswordExpired");
        }
        if (string.IsNullOrWhiteSpace(vm.Password) || string.IsNullOrWhiteSpace(vm.ConfirmPassword))
        {
            base.ViewBag.Error = "Şifre alanları boş olamaz.";
            return View(vm);
        }
        if (vm.Password != vm.ConfirmPassword)
        {
            base.ViewBag.Error = "Şifreler uyuşmuyor.";
            return View(vm);
        }
        string tokenHash = HashResetToken(vm.Token);
        User user = await _context.Users.FirstOrDefaultAsync((User u) => u.PasswordResetToken == tokenHash);
        if (user == null)
        {
            return View("ResetPasswordExpired");
        }
        if (!user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
        {
            return View("ResetPasswordExpired");
        }
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password, workFactor: 12);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await _context.SaveChangesAsync();
        base.TempData["SuccessMessage"] = "Şifreniz güncellendi. Giriş yapabilirsiniz.";
        return RedirectToAction("Login");
    }

    private object? PublicDiagnostics(Dictionary<string, object> diagnostics)
    {
        if (!string.Equals(_config["PythonConfig:DebugMode"], "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] allowedKeys =
        {
            "stage", "contentLength", "sessionPendingUserExists", "userFound",
            "framesCount", "totalFrames", "invalidBase64Frames", "writtenFrames",
            "totalWrittenBytes", "pythonResultKind", "pythonResultLength", "vectorCompareOk"
        };
        return allowedKeys
            .Where(diagnostics.ContainsKey)
            .ToDictionary(key => key, key => diagnostics[key]);
    }

    private static bool IsPythonStatus(string result)
    {
        return result is "NO_FACE" or "NO_BLINK" or "PYTHON_ERROR" or "PYTHON_TIMEOUT";
    }

    private static string HashResetToken(string token)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
