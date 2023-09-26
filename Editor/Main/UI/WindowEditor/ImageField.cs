using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEditor;
using SSL.Data.Utils;
using SSL.Data;

namespace SSL.Graph
{
    public class ImageField : VisualElement
    {
        VisualElement _picker, _imageGroup;
        Button _pickerButton;
        TextElement _pickerTag;
        Slider _rotationSlider;
        string _sourcePath = "";
        string _tmpName; //Name of temporary file created after preprocessing
        string _tmpPath;
        string _tmpRotatedPath;

        Image _image;

        public ImageField(string tagText, string tmpName)
        {
            styleSheets.Add((StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetImageFieldStyle.uss", typeof(StyleSheet)));

            _tmpName = tmpName + ".png";
            _tmpPath = Preprocess.tmp_path + "/" + _tmpName;
            _tmpRotatedPath = Preprocess.tmp_path + "/" + "rotated_" + _tmpName;

            _picker = new VisualElement() 
            {
                name = "picker"
            };
            _pickerButton = new Button()
            {
                name = "pickerButton",
                text = "..."
            };
            _pickerTag = new TextElement()
            {
                name = "pickerTag",
                text = tagText
            };

            _imageGroup = new VisualElement()
            {
                name = "imageGroup"
            };

            _rotationSlider = new Slider()
            {
                name = "pickerSlider",
                lowValue = 0f,
                highValue = 360f,
                direction = SliderDirection.Vertical
            };


            _rotationSlider.RegisterValueChangedCallback(x => RotateImage(x.newValue));


            _picker.Add(_pickerTag);
            _picker.Add(_pickerButton);

            _image = new Image()
            {
                name = "image",
                scaleMode = ScaleMode.StretchToFill
            };

            _pickerButton.clicked += CommenceLoadImage;

            _imageGroup.Add(_image);
            _imageGroup.Add(_rotationSlider);

            this.Add(_picker);
            this.Add(_imageGroup);
        }

        void CommenceLoadImage()
        {
            string filePath = EditorUtility.OpenFilePanel("Please provide an image", "", "");
            if (filePath == string.Empty)
                return;
            Preprocess.PreprocessImage(filePath, _tmpName);
            _sourcePath = filePath;
            EditorApplication.delayCall += FinishLoadingImage;
        }

        void FinishLoadingImage()
        {
            Texture2D loadedImage = Inference.LoadImage(_tmpPath, out (int, int) ogDims);
            _image.image = loadedImage;
            //_image.im
        }

        void RotateImage(float angle)
        {
            Quaternion newQuat = new Quaternion();
            Vector2 imageSize = new Vector2(_image.resolvedStyle.width, _image.resolvedStyle.height);
            Vector2 pivotPoint = imageSize * 0.5f;
            Vector2 rotatedPivot = Quaternion.Euler(0, 0, angle) * pivotPoint;

            Vector2 rotatedDimensions = CalculateRotatedDimensions(_image.resolvedStyle.width, _image.resolvedStyle.height, angle);
            Vector2 scale = new Vector2(_image.resolvedStyle.width / rotatedDimensions.x, _image.resolvedStyle.height / rotatedDimensions.y);

            newQuat.eulerAngles = new Vector3(_image.transform.rotation.eulerAngles.x,
                                              _image.transform.rotation.eulerAngles.y,
                                              angle);
            _image.transform.rotation = newQuat;
            _image.transform.position = new Vector3(-(rotatedPivot.x * scale.x) + pivotPoint.x , -(rotatedPivot.y * scale.y) + pivotPoint.y, 0);
            _image.transform.scale = scale;
        }

        private Vector2 CalculateRotatedDimensions(float width, float height, float rotationAngle)
        {
            float radianAngle = rotationAngle * Mathf.Deg2Rad;
            float cosAngle = Mathf.Abs(Mathf.Cos(radianAngle));
            float sinAngle = Mathf.Abs(Mathf.Sin(radianAngle));

            float newWidth = width * cosAngle + height * sinAngle;
            float newHeight = width * sinAngle + height * cosAngle;

            return new Vector2(newWidth, newHeight);
        }

        public void ApplyTextureRotation()
        {
            TextureRotation((Texture2D)_image.image, _image.transform.rotation);
        }

        private void TextureRotation(Texture2D sourceTexture, Quaternion rotation)
        {
            // Create a new texture to hold the rotated pixels
            Texture2D rotatedTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);

            Color[] sourcePixels = sourceTexture.GetPixels();
            Color[] rotatedPixels = new Color[sourcePixels.Length];

            for (int y = 0; y < sourceTexture.height; y++)
            {
                for (int x = 0; x < sourceTexture.width; x++)
                {
                    Vector2 sourceUV = new Vector2(x / (float)sourceTexture.width, y / (float)sourceTexture.height);
                    Vector2 rotatedUV = RotateUV(sourceUV, rotation);

                    int sourcePixelX = Mathf.FloorToInt(rotatedUV.x * sourceTexture.width);
                    int sourcePixelY = Mathf.FloorToInt(rotatedUV.y * sourceTexture.height);

                    // Ensure the sourcePixelX and sourcePixelY are within valid bounds
                    sourcePixelX = Mathf.Clamp(sourcePixelX, 0, sourceTexture.width - 1);
                    sourcePixelY = Mathf.Clamp(sourcePixelY, 0, sourceTexture.height - 1);

                    int sourcePixelIndex = sourcePixelY * sourceTexture.width + sourcePixelX;

                    rotatedPixels[y * sourceTexture.width + x] = sourcePixels[sourcePixelIndex];
                }
            }

            rotatedTexture.SetPixels(rotatedPixels);
            rotatedTexture.Apply();

            // Save the rotated texture as a PNG file (you can adjust the path as needed)
            byte[] bytes = rotatedTexture.EncodeToPNG();
            string savePath = _tmpRotatedPath;
            System.IO.File.WriteAllBytes(savePath, bytes);
        }

        private Vector2 RotateUV(Vector2 uv, Quaternion rotation)
        {
            Vector3 rotatedPoint = rotation * new Vector3(uv.x * 2 - 1, uv.y * 2 - 1, 0);
            rotatedPoint = rotatedPoint * 0.5f + Vector3.one * 0.5f;
            return new Vector2(rotatedPoint.x, rotatedPoint.y);
        }


        public Texture2D GetImage()
        {
            if (_image.image != null)
                return (Texture2D)_image.image;
            return null;
        }

        public string GetRotatedPath()
        {
            return _tmpRotatedPath;
        }

        public string GetLoadedPath()
        {
            return _tmpPath;
        }

        public string GetSourcePath()
        {
            return _sourcePath;
        }
    }
}