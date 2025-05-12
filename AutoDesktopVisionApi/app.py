\
from flask import Flask, request, jsonify
import base64
import io
# from PIL import Image # PIL/Pillow will be needed later for image processing

app = Flask(__name__)

@app.route('/detect', methods=['POST'])
def detect_objects():
    if 'screenshot' not in request.json:
        return jsonify({"error": "No screenshot data provided"}), 400

    image_data_b64 = request.json['screenshot']
    
    try:
        # Later, you'll decode the image and pass it to YOLOv8
        # For now, we're just acknowledging receipt
        # image_bytes = base64.b64decode(image_data_b64)
        # image = Image.open(io.BytesIO(image_bytes))
        # print(f"Received image of size: {image.size}")

        # Mock YOLOv8 response
        mock_detections = [
            {
                "label": "button",
                "confidence": 0.95,
                "box": [100, 150, 200, 180]  # [xmin, ymin, xmax, ymax]
            },
            {
                "label": "text_input",
                "confidence": 0.88,
                "box": [300, 250, 450, 280]
            },
            {
                "label": "scrollbar",
                "confidence": 0.75,
                "box": [780, 50, 795, 500]
            }
        ]
        
        print(f"Received image data (first 30 chars): {image_data_b64[:30]}...")
        print(f"Returning mock detections: {mock_detections}")
        return jsonify({"detections": mock_detections}), 200

    except Exception as e:
        print(f"Error processing request: {e}")
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    # It's good practice to specify host and port.
    # For development, 0.0.0.0 makes it accessible from your MAUI app (if running on the same machine or network).
    # Ensure your firewall allows connections to this port if MAUI app is on a different device/VM.
    app.run(host='0.0.0.0', port=5001, debug=True)
