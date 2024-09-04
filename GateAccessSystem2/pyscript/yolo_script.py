from ultralytics import YOLO

def detect_objects(image_path):
    model = YOLO('yolov8n.pt')  # Load YOLOv8 model
    results = model(image_path)  # Perform inference
    return results
