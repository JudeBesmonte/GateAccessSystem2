from ultralytics import YOLO
import cv2

# Load YOLOv8 model
model = YOLO('yolov8n.pt')

def detect_license_plate(image_path):
    # Read the image
    frame = cv2.imread(image_path)
    # Perform detection
    results = model(frame)
    return results.pandas().xyxy[0]  # Get bounding boxes in pandas DataFrame

if __name__ == "__main__":
    import sys
    image_path = sys.argv[1]  # Get image path from command-line arguments
    results = detect_license_plate(image_path)
    print(results)
