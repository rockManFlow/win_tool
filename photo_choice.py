import os
import shutil
from pathlib import Path

import cv2
from ultralytics import YOLO


def get_model_path():
    """获取 yolov8s.pt 模型文件路径"""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_dir, "yolov8s.pt")
    if os.path.isfile(model_path):
        return model_path
    # 兼容开发运行目录结构，向上查找项目根目录中的 yolov8s.pt
    probe = script_dir
    for _ in range(5):
        candidate = os.path.join(probe, "yolov8s.pt")
        if os.path.isfile(candidate):
            return candidate
        parent = os.path.dirname(probe)
        if parent == probe:
            break
        probe = parent
    return model_path


def yolov8_ocr_kind():
    """
    获取 YOLOv8s 预训练模型支持的所有类别名称列表
    :return: 类别名称列表（按编号升序排列）
    """
    model_path = get_model_path()
    model = YOLO(model_path)
    # 按编号升序排列
    sorted_classes = sorted(model.names.items(), key=lambda x: x[0])
    return [name for _, name in sorted_classes]


def yolov8_photo_choice(input_dir, output_dir, target_classes, 
                        image_formats=None, log_callback=None):
    """
    相册自动分类工具核心函数
    :param input_dir: 原始图片文件夹路径
    :param output_dir: 筛选后图片保存路径
    :param target_classes: 要筛选的目标类别列表（如['person', 'car']）
    :param image_formats: 支持的图片格式，默认包含jpg/jpeg/png/bmp
    :param log_callback: 日志回调函数，用于输出日志到界面
    :return: 统计结果字典
    """
    if image_formats is None:
        image_formats = ['.jpg', '.jpeg', '.png', '.bmp']
    
    def log(msg):
        if log_callback:
            log_callback(msg)
        else:
            print(msg, flush=True)
    
    # 初始化YOLOv8模型
    model_path = get_model_path()
    if not os.path.isfile(model_path):
        raise FileNotFoundError(f"未找到模型文件: {model_path}")
    
    log(f"正在加载 YOLO 模型: {model_path}")
    model = YOLO(model_path)

    # 初始化统计变量
    total_images = 0
    matched_images = 0
    unmatched_images = 0
    error_images = []

    # 确保输出文件夹存在
    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # 获取所有待处理文件
    all_files = [f for f in os.listdir(input_dir) 
                 if not os.path.isdir(os.path.join(input_dir, f)) 
                 and Path(f).suffix.lower() in image_formats]
    
    log(f"找到 {len(all_files)} 张图片待处理")
    log(f"筛选目标类别: {target_classes}")

    # 遍历输入文件夹下的所有文件
    for file_name in all_files:
        file_path = os.path.join(input_dir, file_name)
        file_ext = Path(file_name).suffix.lower()

        total_images += 1
        log(f"正在处理 ({total_images}/{len(all_files)}): {file_name}")

        try:
            # 读取图片
            img = cv2.imread(file_path)
            if img is None:
                raise Exception("cv2 无法读取图片")

            # 使用 YOLO 识别图片中的目标
            results = model(img, verbose=False)

            # 检查是否包含目标类别
            has_target = False
            detected_classes = []
            for result in results:
                for box in result.boxes:
                    cls_name = model.names[int(box.cls[0])]
                    detected_classes.append(cls_name)
                    if cls_name in target_classes:
                        has_target = True

            # 根据识别结果处理图片
            if has_target:
                # 符合条件：移动到输出文件夹
                output_path = os.path.join(output_dir, file_name)
                # 处理重名文件
                if os.path.exists(output_path):
                    file_stem = Path(file_name).stem
                    counter = 1
                    while os.path.exists(output_path):
                        output_path = os.path.join(output_dir, f"{file_stem}_copy{counter}{file_ext}")
                        counter += 1

                shutil.move(file_path, output_path)
                matched_images += 1
                log(f"  -> 包含目标类别，已移动到输出文件夹")
            else:
                unmatched_images += 1
                log(f"  -> 不包含目标类别，保留在原文件夹")

        except Exception as e:
            error_images.append((file_name, str(e)))
            unmatched_images += 1
            log(f"  -> 处理失败: {e}")

    # 生成统计结果
    stats = {
        "total": total_images,
        "matched": matched_images,
        "unmatched": unmatched_images,
        "errors": len(error_images),
        "error_list": error_images
    }

    # 打印统计报告
    log("=" * 50)
    log("相册分类统计报告")
    log("=" * 50)
    log(f"原始文件夹: {input_dir}")
    log(f"输出文件夹: {output_dir}")
    log(f"筛选目标: {target_classes}")
    log(f"总图片数: {total_images}")
    log(f"符合条件并移动的图片数: {matched_images}")
    log(f"不符合条件的图片数: {unmatched_images}")
    if error_images:
        log(f"处理失败的图片数: {len(error_images)}")
    log("=" * 50)

    return stats


if __name__ == '__main__':
    # 测试获取类别
    print("YOLOv8s 支持的类别:")
    kinds = yolov8_ocr_kind()
    for i, name in enumerate(kinds):
        print(f"  {i}: {name}")
    print(f"总计: {len(kinds)} 类")
