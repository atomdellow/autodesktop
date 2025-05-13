from ultralytics import YOLO
import os
import torch

# --- Configuration ---
# Path to your data.yaml file
# IMPORTANT: Adjust this path if your train_yolo.py script is not in AutoDesktopVisionApi
# or if data.yaml is located elsewhere.
DATA_YAML_PATH = os.path.join(os.path.dirname(__file__), '..', 'AutoDesktopApplication', 'UI_Element_Dataset', 'data.yaml')
# Pre-trained model to start from (e.g., 'yolov8n.pt', 'yolov8s.pt')
# This file should be in the same directory as this script, or provide a full path.
BASE_MODEL_NAME = 'yolov8n.pt'
# Number of training epochs
EPOCHS = 50
# Batch size (how many images to process at once). Adjust based on your GPU memory.
# If you get out-of-memory errors, try reducing this.
BATCH_SIZE = 8 # Default is 16, but 8 might be safer for various hardware
# Image size for training. YOLOv8 will automatically handle resizing.
# Common sizes are 640, 1280. Larger sizes can improve accuracy but require more memory and time.
IMG_SIZE = 640
# Project name for saving results (will create a folder 'runs/detect/PROJECT_NAME')
PROJECT_NAME = 'UI_Element_Detection'
# Experiment name (will create a subfolder 'runs/detect/PROJECT_NAME/EXPERIMENT_NAME')
EXPERIMENT_NAME = 'run1'
# Device to train on: 'cpu', 'cuda', or 'mps' for Apple Silicon
# Set to 'cuda' if you have an NVIDIA GPU and CUDA installed.
# Set to 'mps' if you have an Apple M-series chip.
# Otherwise, it will default to 'cpu', which will be very slow.
DEVICE = 'cuda' if torch.cuda.is_available() else 'mps' if torch.backends.mps.is_available() else 'cpu'

def train_model():
    """
    Loads the YOLO model, trains it on the custom dataset, and saves the results.
    """
    print(f"Starting training with the following configuration:")
    print(f"  Data YAML: {DATA_YAML_PATH}")
    print(f"  Base Model: {BASE_MODEL_NAME}")
    print(f"  Epochs: {EPOCHS}")
    print(f"  Batch Size: {BATCH_SIZE}")
    print(f"  Image Size: {IMG_SIZE}")
    print(f"  Project: {PROJECT_NAME}")
    print(f"  Experiment: {EXPERIMENT_NAME}")
    print(f"  Device: {DEVICE}")

    # Check if the base model file exists
    if not os.path.exists(BASE_MODEL_NAME):
        print(f"Error: Base model '{BASE_MODEL_NAME}' not found in the current directory ({os.getcwd()}).")
        print("Please make sure the .pt file is in the same directory as this script, or update BASE_MODEL_NAME.")
        return

    # Check if the data.yaml file exists
    if not os.path.exists(DATA_YAML_PATH):
        print(f"Error: data.yaml not found at '{DATA_YAML_PATH}'.")
        print("Please verify the DATA_YAML_PATH variable in this script.")
        return

    try:
        # Load the YOLO model.
        # This will download the model if it's a standard one like 'yolov8n.pt' and not found locally,
        # or load it from the specified path.
        model = YOLO(BASE_MODEL_NAME)
        print(f"Successfully loaded base model: {BASE_MODEL_NAME}")

        # Train the model
        print("Starting model training...")
        results = model.train(
            data=DATA_YAML_PATH,
            epochs=EPOCHS,
            imgsz=IMG_SIZE,
            batch=BATCH_SIZE,
            project=PROJECT_NAME,
            name=EXPERIMENT_NAME,
            device=DEVICE,
            exist_ok=True # Allows overwriting if the experiment name already exists
        )
        
        print("Training completed!")
        print(f"Results saved to: {results.save_dir}")
        print(f"The best model is saved as: {os.path.join(results.save_dir, 'weights', 'best.pt')}")
        print("You can now use this 'best.pt' model in your app.py for inference.")

    except Exception as e:
        print(f"An error occurred during training: {e}")
        print("Please check your dataset, configuration, and environment.")
        if "out of memory" in str(e).lower():
            print("Suggestion: Try reducing BATCH_SIZE or IMG_SIZE if you encountered an out-of-memory error.")

if __name__ == '__main__':
    # This ensures the training process starts only when the script is executed directly.
    train_model()
    # Example of how to run this script from the terminal, assuming you are in the AutoDesktopVisionApi directory:
    # python train_yolo.py
