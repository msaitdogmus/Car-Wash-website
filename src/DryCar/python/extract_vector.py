import sys
import os
import argparse
import traceback
import numpy as np
import face_recognition

try:
    import cv2
except ImportError:
    cv2 = None


def eprint(*args, **kwargs):
    print(*args, file=sys.stderr, **kwargs)


def variance_of_laplacian(gray):
    return cv2.Laplacian(gray, cv2.CV_64F).var()


def pick_best_face(face_locations):
    if not face_locations:
        return None, None

    best_idx = 0
    best_area = -1

    for i, loc in enumerate(face_locations):
        top, right, bottom, left = loc
        w = max(0, right - left)
        h = max(0, bottom - top)
        area = w * h

        if area > best_area:
            best_area = area
            best_idx = i

    return best_idx, best_area


def euclid(a, b):
    ax, ay = a
    bx, by = b
    return float(((ax - bx) ** 2 + (ay - by) ** 2) ** 0.5)


def eye_aspect_ratio(eye_pts):
    if eye_pts is None:
        return None
    if len(eye_pts) != 6:
        return None

    p1, p2, p3, p4, p5, p6 = eye_pts

    denom = 2.0 * euclid(p1, p4)
    if denom <= 1e-6:
        return None

    return (euclid(p2, p6) + euclid(p3, p5)) / denom


def debug_file_info(path, prefix="FILE"):
    try:
        exists = os.path.exists(path)
        size = os.path.getsize(path) if exists else 0
        eprint(f"{prefix}: path={path}")
        eprint(f"{prefix}: exists={exists}")
        eprint(f"{prefix}: size={size}")
    except Exception as ex:
        eprint(f"{prefix}: info_error={repr(ex)}")


def detect_blink_in_images(
    image_paths,
    model,
    ear_open=0.23,
    ear_closed=0.20,
    debug=False
):
    saw_open = False
    saw_closed = False
    best_open_path = None
    best_open_ear = -1.0

    if debug:
        eprint("BLINK_CHECK_START")
        eprint(f"image_count={len(image_paths)}")
        eprint(f"model={model}")
        eprint(f"ear_open={ear_open}")
        eprint(f"ear_closed={ear_closed}")

    for idx, path in enumerate(image_paths):
        try:
            if debug:
                eprint(f"BLINK_FRAME_INDEX={idx}")
                debug_file_info(path, prefix=f"BLINK_FRAME_{idx}")

            if not os.path.exists(path):
                if debug:
                    eprint(f"BLINK_FRAME_{idx}: file_not_found")
                continue

            img = face_recognition.load_image_file(path)

            if debug:
                try:
                    eprint(f"BLINK_FRAME_{idx}: shape={img.shape}")
                    eprint(f"BLINK_FRAME_{idx}: dtype={img.dtype}")
                except Exception:
                    pass

            face_locations = face_recognition.face_locations(img, model=model)

            if debug:
                eprint(f"BLINK_FRAME_{idx}: face_count={len(face_locations)}")

            if not face_locations:
                continue

            best_idx, best_area = pick_best_face(face_locations)
            if best_idx is None:
                if debug:
                    eprint(f"BLINK_FRAME_{idx}: best_face_none")
                continue

            if debug:
                eprint(f"BLINK_FRAME_{idx}: best_face_idx={best_idx}")
                eprint(f"BLINK_FRAME_{idx}: best_face_area={best_area}")
                eprint(f"BLINK_FRAME_{idx}: best_face_loc={face_locations[best_idx]}")

            best_loc = [face_locations[best_idx]]

            lms = face_recognition.face_landmarks(img, face_locations=best_loc)
            if not lms:
                if debug:
                    eprint(f"BLINK_FRAME_{idx}: landmarks_empty")
                continue

            lm = lms[0]
            left_eye = lm.get("left_eye")
            right_eye = lm.get("right_eye")

            if left_eye is None or right_eye is None:
                if debug:
                    eprint(f"BLINK_FRAME_{idx}: eye_landmarks_missing")
                continue

            le = eye_aspect_ratio(left_eye)
            re = eye_aspect_ratio(right_eye)

            if le is None or re is None:
                if debug:
                    eprint(f"BLINK_FRAME_{idx}: ear_calc_failed")
                continue

            ear = (le + re) / 2.0

            if debug:
                eprint(f"BLINK_FRAME_{idx}: leftEAR={le:.4f}")
                eprint(f"BLINK_FRAME_{idx}: rightEAR={re:.4f}")
                eprint(f"BLINK_FRAME_{idx}: avgEAR={ear:.4f}")

            if ear >= ear_open:
                saw_open = True
                if ear > best_open_ear:
                    best_open_ear = ear
                    best_open_path = path
            elif ear <= ear_closed:
                saw_closed = True

            if debug:
                eprint(f"BLINK_FRAME_{idx}: saw_open={saw_open}")
                eprint(f"BLINK_FRAME_{idx}: saw_closed={saw_closed}")
                eprint(f"BLINK_FRAME_{idx}: best_open_path={best_open_path}")

            if saw_open and saw_closed:
                if best_open_path is None:
                    best_open_path = path

                if debug:
                    eprint("BLINK_RESULT=SUCCESS")
                    eprint(f"BLINK_BEST_OPEN_PATH={best_open_path}")

                return True, best_open_path

        except Exception as ex:
            if debug:
                eprint(f"BLINK_FRAME_{idx}: exception={repr(ex)}")
                eprint(traceback.format_exc())
            continue

    if debug:
        eprint("BLINK_RESULT=FAIL")
        eprint(f"BLINK_FINAL_saw_open={saw_open}")
        eprint(f"BLINK_FINAL_saw_closed={saw_closed}")
        eprint(f"BLINK_FINAL_best_open_path={best_open_path}")

    return False, best_open_path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("image_paths", nargs="+")
    parser.add_argument("--blink", action="store_true", help="Liveness: blink kontrolü zorunlu")
    parser.add_argument("--ear_open", type=float, default=0.20)
    parser.add_argument("--ear_closed", type=float, default=0.18)
    parser.add_argument("--model", default="hog", choices=["hog", "cnn"])
    parser.add_argument("--min_face_area", type=int, default=80 * 80, help="px^2")
    parser.add_argument("--min_brightness", type=float, default=40.0)
    parser.add_argument("--max_brightness", type=float, default=220.0)
    parser.add_argument("--min_sharpness", type=float, default=25.0, help="Laplacian variance")
    parser.add_argument("--debug", action="store_true")
    args = parser.parse_args()

    try:
        image_paths = args.image_paths

        if args.debug:
            eprint("PYTHON_START")
            eprint(f"python_executable={sys.executable}")
            eprint(f"python_version={sys.version}")
            eprint(f"cwd={os.getcwd()}")
            eprint(f"argv={sys.argv}")
            eprint(f"cv2_installed={cv2 is not None}")
            eprint(f"numpy_version={np.__version__}")
            try:
                eprint(f"face_recognition_file={face_recognition.__file__}")
            except Exception:
                pass
            eprint(f"image_path_count={len(image_paths)}")

            for i, p in enumerate(image_paths):
                debug_file_info(p, prefix=f"INPUT_{i}")

        if args.blink:
            ok, best_open_path = detect_blink_in_images(
                image_paths=image_paths,
                model=args.model,
                ear_open=args.ear_open,
                ear_closed=args.ear_closed,
                debug=args.debug
            )

            if not ok:
                if args.debug:
                    eprint("NO_BLINK: open/closed transition not found")
                print("NO_BLINK")
                return

            if best_open_path:
                image_paths = [best_open_path]
                if args.debug:
                    eprint(f"ENCODING_IMAGE_SELECTED={best_open_path}")

        first_path = image_paths[0]

        if args.debug:
            debug_file_info(first_path, prefix="ENCODING_INPUT")

        if not os.path.exists(first_path):
            if args.debug:
                eprint("NO_FACE: encoding input file not found")
            print("NO_FACE")
            return

        image = face_recognition.load_image_file(first_path)

        if args.debug:
            try:
                eprint(f"ENCODING_IMAGE_SHAPE={image.shape}")
                eprint(f"ENCODING_IMAGE_DTYPE={image.dtype}")
            except Exception:
                pass

        face_locations = face_recognition.face_locations(image, model=args.model)

        if args.debug:
            eprint(f"ENCODING_FACE_COUNT={len(face_locations)}")

        if not face_locations:
            if args.debug:
                eprint("NO_FACE: face_locations empty")
            print("NO_FACE")
            return

        best_idx, best_area = pick_best_face(face_locations)

        if args.debug:
            eprint(f"ENCODING_BEST_FACE_IDX={best_idx}")
            eprint(f"ENCODING_BEST_FACE_AREA={best_area}")
            if best_idx is not None:
                eprint(f"ENCODING_BEST_FACE_LOC={face_locations[best_idx]}")

        if best_area is None or best_area < args.min_face_area:
            if args.debug:
                eprint(f"NO_FACE: face too small area={best_area}, min_required={args.min_face_area}")
            print("NO_FACE")
            return

        best_location = [face_locations[best_idx]]

        if cv2 is not None:
            bgr = image[:, :, ::-1]
            gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)

            top, right, bottom, left = face_locations[best_idx]
            h_img, w_img = gray.shape[:2]

            if top < 0:
                top = 0
            if left < 0:
                left = 0
            if bottom > h_img:
                bottom = h_img
            if right > w_img:
                right = w_img

            if top > h_img - 1:
                top = h_img - 1
            if left > w_img - 1:
                left = w_img - 1

            if bottom <= top or right <= left:
                if args.debug:
                    eprint("NO_FACE: invalid ROI bounds")
                    eprint(f"ROI=top:{top}, right:{right}, bottom:{bottom}, left:{left}")
                print("NO_FACE")
                return

            face_gray = gray[top:bottom, left:right]

            brightness = float(np.mean(face_gray))
            sharpness = float(variance_of_laplacian(face_gray))

            if args.debug:
                eprint(f"ROI_BRIGHTNESS={brightness}")
                eprint(f"ROI_SHARPNESS={sharpness}")
                eprint(f"ROI_HEIGHT={face_gray.shape[0]}")
                eprint(f"ROI_WIDTH={face_gray.shape[1]}")
                eprint(f"MIN_BRIGHTNESS={args.min_brightness}")
                eprint(f"MAX_BRIGHTNESS={args.max_brightness}")
                eprint(f"MIN_SHARPNESS={args.min_sharpness}")

            if brightness < args.min_brightness or brightness > args.max_brightness:
                if args.debug:
                    eprint(f"NO_FACE: brightness out of range (face ROI)={brightness:.1f}")
                print("NO_FACE")
                return

            if sharpness < args.min_sharpness:
                if args.debug:
                    eprint(f"NO_FACE: too blurry (face ROI) sharpness={sharpness:.1f}")
                print("NO_FACE")
                return
        else:
            if args.debug:
                eprint("WARN: cv2 not installed; skipping brightness/sharpness checks")

        encodings = face_recognition.face_encodings(image, known_face_locations=best_location)

        if args.debug:
            eprint(f"ENCODING_VECTOR_COUNT={len(encodings)}")

        if not encodings:
            if args.debug:
                eprint("NO_FACE: encodings empty")
            print("NO_FACE")
            return

        encoding = encodings[0]
        vector_str = ",".join(f"{x:.6f}" for x in encoding.tolist())

        if args.debug:
            eprint(f"VECTOR_LENGTH_CHARS={len(vector_str)}")

        print(vector_str)

    except Exception as e:
        if "--debug" in sys.argv:
            eprint("EXCEPTION:", repr(e))
            eprint(traceback.format_exc())
        print("NO_FACE")


if __name__ == "__main__":
    main()