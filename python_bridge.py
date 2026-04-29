import argparse
import hashlib
import json
import os
import sys
from datetime import datetime

# 将脚本所在目录添加到模块搜索路径，确保能导入同目录下的模块
script_dir = os.path.dirname(os.path.abspath(__file__))
if script_dir not in sys.path:
    sys.path.insert(0, script_dir)


def log(message: str) -> None:
    print(message, flush=True)


def result(success: bool, message: str) -> int:
    payload = {"success": success, "message": message}
    print(f"RESULT_JSON:{json.dumps(payload, ensure_ascii=False)}", flush=True)
    return 0 if success else 1


def convert_size(size_bytes: int) -> str:
    if size_bytes <= 0:
        return "0 B"
    size_names = ("B", "KB", "MB", "GB", "TB")
    size = float(size_bytes)
    idx = 0
    while size >= 1024 and idx < len(size_names) - 1:
        size /= 1024.0
        idx += 1
    return f"{size:.2f} {size_names[idx]}"


def get_file_md5(file_path: str) -> str:
    with open(file_path, "rb") as f:
        return hashlib.md5(f.read()).hexdigest()


def cmd_video_extract(video_path: str, output_dir: str) -> int:
    try:
        import cv2
    except Exception as ex:
        return result(False, f"导入 cv2 失败: {ex}")

    try:
        if not os.path.exists(video_path):
            return result(False, f"视频文件不存在: {video_path}")

        if not os.path.exists(output_dir):
            os.makedirs(output_dir, exist_ok=True)

        cap = cv2.VideoCapture(video_path)
        if not cap.isOpened():
            return result(False, "视频无法打开，请检查格式或路径。")

        frame_count = 0
        log("正在提取帧（请稍候）...")
        while True:
            ret, frame = cap.read()
            if not ret:
                break
            frame_path = os.path.join(output_dir, f"frame_{frame_count:06d}.png")
            cv2.imwrite(frame_path, frame)
            frame_count += 1

        cap.release()
        log(f"共提取 {frame_count} 帧图片")
        return result(True, f"提取完成，输出路径：{output_dir}")
    except Exception as ex:
        return result(False, f"提取失败: {ex}")


def cmd_image_dedup(folder_path: str, delete_dup: bool) -> int:
    try:
        from PIL import Image
        import imagehash
    except Exception as ex:
        return result(False, f"导入图片库失败: {ex}")

    if not os.path.isdir(folder_path):
        return result(False, f"文件夹不存在: {folder_path}")

    md5_dict = {}
    phash_dict = {}
    conform_count = 0
    dup_count = 0
    del_count = 0

    log("正在检测文件夹内的 png/jpg/jpeg/webp 重复图片")
    for root, _, files in os.walk(folder_path):
        for file_name in files:
            if not file_name.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                continue
            conform_count += 1
            path = os.path.join(root, file_name)
            try:
                file_md5 = get_file_md5(path)
                if file_md5 in md5_dict:
                    dup_count += 1
                    log(f"完全重复文件: {path} <=> {md5_dict[file_md5]}")
                    if delete_dup:
                        os.remove(path)
                        del_count += 1
                    continue

                with Image.open(path) as img:
                    img_phash = imagehash.phash(img)

                is_dup = False
                for existing_phash, existing_path in phash_dict.items():
                    if img_phash - existing_phash < 5:
                        dup_count += 1
                        log(f"相似图片: {path} ~ {existing_path}")
                        if delete_dup:
                            os.remove(path)
                            del_count += 1
                        is_dup = True
                        break

                if not is_dup:
                    phash_dict[img_phash] = path
                    md5_dict[file_md5] = path
            except Exception as ex:
                return result(False, f"处理失败 {path}: {ex}")

    if delete_dup and dup_count > 0:
        return result(
            True,
            f"去重完成！满足条件的图片共 {conform_count} 张，共检测到 {dup_count + 1} 张重复图片，已删除 {del_count} 张，保留 1 张",
        )
    if dup_count > 0:
        return result(True, f"去重完成！满足条件的图片共 {conform_count} 张，共检测到 {dup_count + 1} 张重复图片（未删除）")
    return result(True, f"去重完成！满足条件的图片共 {conform_count} 张，未检测到重复图片")


def get_file_size(path: str) -> int:
    try:
        if os.path.islink(path):
            return 0
        return os.path.getsize(path)
    except Exception:
        return 0


def calculate_path_size(target_path: str, threshold: int) -> str:
    if not os.path.exists(target_path):
        return convert_size(0)

    if os.path.isfile(target_path):
        file_size = get_file_size(target_path)
        if file_size > threshold:
            return convert_size(file_size)
        return convert_size(0)

    total_size = 0
    for root, _, files in os.walk(target_path):
        for file_name in files:
            file_path = os.path.join(root, file_name)
            file_size = get_file_size(file_path)
            total_size += file_size
            if file_size > threshold:
                log(f"文件: {file_path:<60} {convert_size(file_size)}")
    return convert_size(total_size)


def cmd_file_size(target_path: str, threshold: int) -> int:
    try:
        if not os.path.exists(target_path):
            return result(False, f"路径不存在: {target_path}")
        log("程序处理中，请稍后...")
        size_result = calculate_path_size(target_path, threshold)
        if os.path.isfile(target_path):
            return result(True, f"文件 {os.path.basename(target_path)} 大小：{size_result}")
        return result(True, f"文件夹 {target_path} 总大小：{size_result}")
    except Exception as ex:
        return result(False, f"统计异常：{ex}")


def cmd_speak_loop(text: str, interval_seconds: float) -> int:
    try:
        import pyttsx3
        import time
    except Exception as ex:
        return result(False, f"导入语音库失败: {ex}")

    if not text.strip():
        return result(True, "提醒内容为空，跳过播报。")

    engine = pyttsx3.init()
    engine.setProperty("rate", 200)
    engine.setProperty("volume", 1.0)
    log("语音播报已启动。")
    try:
        while True:
            engine.say(text)
            engine.runAndWait()
            time.sleep(max(interval_seconds, 0.2))
    except KeyboardInterrupt:
        pass
    except Exception as ex:
        return result(False, f"语音播报异常: {ex}")
    finally:
        engine.stop()

    return result(True, "语音播报已结束。")


def cmd_camera_probe(camera_index: int) -> int:
    try:
        import cv2
    except Exception as ex:
        return result(False, f"导入 cv2 失败: {ex}")

    cap = cv2.VideoCapture(camera_index)
    if not cap.isOpened():
        return result(False, "摄像头不可用，请检查设备或权限。")

    try:
        ok, frame = cap.read()
        if not ok or frame is None:
            return result(False, "摄像头打开成功，但无法读取画面。")
        return result(True, "摄像头可用")
    except Exception as ex:
        return result(False, f"摄像头检测失败: {ex}")
    finally:
        cap.release()


def cmd_photo_choice(input_dir: str, output_dir: str, target_class: str) -> int:
    try:
        from photo_choice import yolov8_photo_choice
    except Exception as ex:
        return result(False, f"导入 photo_choice 模块失败: {ex}")

    if not os.path.isdir(input_dir):
        return result(False, f"输入文件夹不存在: {input_dir}")

    try:
        log(f"开始图片分类...")
        log(f"输入文件夹: {input_dir}")
        log(f"输出文件夹: {output_dir}")
        log(f"筛选类别: {target_class}")
        
        stats = yolov8_photo_choice(
            input_dir=input_dir,
            output_dir=output_dir,
            target_classes=[target_class],
            log_callback=log
        )
        
        msg = f"分类完成！总计 {stats['total']} 张图片，符合条件 {stats['matched']} 张，不符合 {stats['unmatched']} 张"
        if stats['errors'] > 0:
            msg += f"，处理失败 {stats['errors']} 张"
        return result(True, msg)
    except Exception as ex:
        return result(False, f"图片分类失败: {ex}")


def cmd_get_photo_kinds() -> int:
    """获取 YOLOv8 支持的所有类别名称"""
    try:
        from photo_choice import yolov8_ocr_kind
    except Exception as ex:
        return result(False, f"导入 photo_choice 模块失败: {ex}")

    try:
        kinds = yolov8_ocr_kind()
        # 返回 JSON 格式的类别列表
        payload = {"success": True, "message": "获取成功", "kinds": kinds}
        print(f"RESULT_JSON:{json.dumps(payload, ensure_ascii=False)}", flush=True)
        return 0
    except Exception as ex:
        return result(False, f"获取类别失败: {ex}")


def cmd_crowd_count(camera_index: int) -> int:
    try:
        import cv2
        from ultralytics import YOLO
    except Exception as ex:
        return result(False, f"导入依赖失败: {ex}")

    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_dir, "yolov8s.pt")
    if not os.path.isfile(model_path):
        # 兼容开发运行目录结构，向上查找项目根目录中的 yolov8s.pt
        probe = script_dir
        for _ in range(5):
            candidate = os.path.join(probe, "yolov8s.pt")
            if os.path.isfile(candidate):
                model_path = candidate
                break
            parent = os.path.dirname(probe)
            if parent == probe:
                break
            probe = parent

    if not os.path.isfile(model_path):
        return result(False, "未找到模型文件 yolov8s.pt，请放在项目目录中。")

    try:
        model = YOLO(model_path)
    except Exception as ex:
        return result(False, f"加载 YOLO 模型失败: {ex}")

    cap = cv2.VideoCapture(camera_index)
    if not cap.isOpened():
        return result(False, "摄像头不可用，请检查设备或权限。")

    try:
        ok, frame = cap.read()
        if not ok or frame is None:
            return result(False, "无法读取摄像头画面。")

        predictions = model.predict(
            source=frame,
            classes=[0],  # COCO: person
            conf=0.25,
            verbose=False,
            imgsz=640
        )
        boxes = predictions[0].boxes if predictions else None
        count = 0 if boxes is None else len(boxes)
        return result(True, str(count))
    except Exception as ex:
        return result(False, f"统计失败: {ex}")
    finally:
        cap.release()


def main() -> int:
    parser = argparse.ArgumentParser(description="WinTool Python bridge")
    subparsers = parser.add_subparsers(dest="command", required=True)

    p_video = subparsers.add_parser("video_extract")
    p_video.add_argument("--video", required=True)
    p_video.add_argument("--output", required=True)

    p_dedup = subparsers.add_parser("image_dedup")
    p_dedup.add_argument("--folder", required=True)
    p_dedup.add_argument("--delete", default="false")

    p_size = subparsers.add_parser("file_size")
    p_size.add_argument("--path", required=True)
    p_size.add_argument("--threshold", type=int, default=0)

    p_speak = subparsers.add_parser("speak_loop")
    p_speak.add_argument("--text", required=True)
    p_speak.add_argument("--interval", type=float, default=1.0)

    p_crowd = subparsers.add_parser("crowd_count")
    p_crowd.add_argument("--camera", type=int, default=0)
    p_probe = subparsers.add_parser("camera_probe")
    p_probe.add_argument("--camera", type=int, default=0)

    p_photo_choice = subparsers.add_parser("photo_choice")
    p_photo_choice.add_argument("--input", required=True)
    p_photo_choice.add_argument("--output", required=True)
    p_photo_choice.add_argument("--class", dest="target_class", required=True)

    p_photo_kinds = subparsers.add_parser("get_photo_kinds")

    args = parser.parse_args()
    log(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] Python 任务启动: {args.command}")

    if args.command == "video_extract":
        return cmd_video_extract(args.video, args.output)
    if args.command == "image_dedup":
        delete_dup = str(args.delete).lower() in ("1", "true", "yes", "y")
        return cmd_image_dedup(args.folder, delete_dup)
    if args.command == "file_size":
        return cmd_file_size(args.path, args.threshold)
    if args.command == "speak_loop":
        return cmd_speak_loop(args.text, args.interval)
    if args.command == "crowd_count":
        return cmd_crowd_count(args.camera)
    if args.command == "camera_probe":
        return cmd_camera_probe(args.camera)
    if args.command == "photo_choice":
        return cmd_photo_choice(args.input, args.output, args.target_class)
    if args.command == "get_photo_kinds":
        return cmd_get_photo_kinds()
    return result(False, "未知命令")


if __name__ == "__main__":
    sys.exit(main())
