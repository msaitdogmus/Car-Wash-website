(() => {
    "use strict";

    const root = document.querySelector("[data-face-verification]");
    if (!root) return;

    const video = root.querySelector("video");
    const canvas = root.querySelector("canvas");
    const startButton = root.querySelector("[data-start]");
    const status = root.querySelector("[data-status]");
    const endpoint = root.dataset.endpoint;
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value;

    let stream;
    let running = false;

    function setStatus(message, kind = "info") {
        status.textContent = message;
        status.className = `alert alert-${kind}`;
    }

    function stopCamera() {
        stream?.getTracks().forEach(track => track.stop());
        stream = undefined;
        video.srcObject = null;
    }

    async function startCamera() {
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "user", width: { ideal: 960 }, height: { ideal: 720 } },
                audio: false
            });
            video.srcObject = stream;
            await video.play();
            startButton.disabled = false;
            setStatus("Hazır olduğunuzda başlayın. Komut gelince bir kez göz kırpın.", "success");
        } catch {
            setStatus("Kamera açılamadı. Tarayıcı izinlerini kontrol edin.", "danger");
        }
    }

    async function captureFrames() {
        const context = canvas.getContext("2d", { alpha: false });
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        const frames = [];
        const startedAt = performance.now();
        while (performance.now() - startedAt < 3500) {
            context.drawImage(video, 0, 0, canvas.width, canvas.height);
            frames.push(canvas.toDataURL("image/jpeg", 0.82));
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        return frames;
    }

    async function verify() {
        if (running || !endpoint || !token) return;
        running = true;
        startButton.disabled = true;
        setStatus("3… 2… 1… Şimdi bir kez göz kırpın.", "warning");

        try {
            const frames = await captureFrames();
            const response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token
                },
                body: JSON.stringify({
                    FaceBase64: frames.at(-1),
                    FramesBase64: frames
                })
            });

            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const result = await response.json();

            if (result.success) {
                setStatus(result.message || "Doğrulama başarılı.", "success");
                stopCamera();
                window.setTimeout(() => window.location.assign("/"), 1000);
                return;
            }

            setStatus(result.message || "Yüz doğrulanamadı. Tekrar deneyin.", "danger");
        } catch {
            setStatus("Doğrulama sırasında bağlantı hatası oluştu.", "danger");
        } finally {
            running = false;
            startButton.disabled = false;
        }
    }

    startButton.addEventListener("click", verify);
    window.addEventListener("beforeunload", stopCamera);
    startCamera();
})();
