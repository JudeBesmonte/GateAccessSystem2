from ultralytics import YOLO
import os

def run_detection(image_path):
    # Load the model
    model = YOLO("yolov8n.pt")
    
    # Perform inference
    results = model(image_path)
    
    # Handle the results
    if results and hasattr(results[0], 'save'):
        save_dir = "runs/detect/predict"
        results[0].save(save_dir=save_dir)  # Save the results; results is a list, so access the first item
        
        # Construct the path to the saved image
        detected_image_path = os.path.join(save_dir, os.path.basename(image_path))
        print(f"Detected image path: {detected_image_path}")  # Debug print
        return detected_image_path
    else:
        print("No results or results object does not have 'save' method")
        return ""

if __name__ == "__main__":
    import sys
    image_path = sys.argv[1]
    detected_image_path = run_detection(image_path)
    print(detected_image_path)  # This line sends the path back to C#





