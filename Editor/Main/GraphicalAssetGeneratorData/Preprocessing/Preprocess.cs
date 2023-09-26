using UnityEngine;
using System;
using Unity.Barracuda;
using UnityEditor;
using System.IO;

namespace SSL.Data.Utils
{
    public class Preprocess
    {
        public static string tmp_path =         $"{GAGenDataUtils.BasePath}Editor/Main/GraphicalAssetGeneratorData/Preprocessing/tmp";
        //public static string PreprocessImage(string imagePath, string tmpID)
        //{
        //    string toolPath = $"{Application.dataPath}/Scripts/SwordShapeLanguage/Editor/Main/GraphicalAssetGeneratorData/Preprocessing/preprocess_for_unity1file.exe";
        //    string arguments = $"--image_path {imagePath} --image_temp_path {tmp_path}/{tmpID}";
            
        //    Process process = new Process();
        //    process.StartInfo.FileName = toolPath;
        //    process.StartInfo.Arguments = arguments;
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.CreateNoWindow = true;
        //    process.Start();

        //    // Read the output of the executable
        //    string result = process.StandardOutput.ReadToEnd();

        //    process.WaitForExit();

        //    return result;
        //}

        public static string PreprocessImage(string imagePath, string tmpID)
        {
            NNModel modelAssetBGRemove;
            Model bgRemover;
            IWorker workerBG;
            modelAssetBGRemove = (NNModel)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/ONNX/u2net.onnx", typeof(NNModel));
            bgRemover = ModelLoader.Load(modelAssetBGRemove);
            workerBG = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, bgRemover);
            Texture2D image = Inference.LoadImage(imagePath, out (int, int) ogDims, 320);
            var imageData = Inference.LoadChannelAsFloatArray(image, 3);
            Tensor imageTensor = new Tensor(1, 320, 320, 3, imageData);

            //Inference
            workerBG.Execute(imageTensor);
            Tensor result = workerBG.PeekOutput("1959");

            //Normalise
            float rMin = 0f;
            float rMax = 0f;
            for (int i = 0; i < result.length; i++)
            {
                if(result[i] > rMax)
                    rMax = result[i];
                if (result[i] < rMin)
                    rMin = result[i];
            }

            for (int i = 0; i < result.length; i++)
            {
                result[i] = (result[i] - rMin) / (rMax - rMin);
            }

            //Convert to render texture then to texture2d
            var resultRT = result.ToRenderTexture(scale: 255);
            Texture2D maskTexture = new Texture2D(320, 320, TextureFormat.RGB24, false);
            RenderTexture.active = resultRT;
            maskTexture.ReadPixels(new Rect(0, 0, resultRT.width, resultRT.height), 0, 0);
            maskTexture.Apply();

            Color[] maskPixels = maskTexture.GetPixels();
            Color[] flippedPixels = new Color[maskTexture.width * maskTexture.height];
            Color[] rotatedPixels = new Color[maskTexture.width * maskTexture.height];

            for (int y = 0; y < maskTexture.height; y++)
            {
                for (int x = 0; x < maskTexture.width; x++)
                {
                    int sourceX = maskTexture.width - 1 - x;
                    int sourceY = y;

                    flippedPixels[y * maskTexture.width + x] = maskPixels[sourceY * maskTexture.width + sourceX];
                }
            }

            for (int y = 0; y < maskTexture.height; y++)
            {
                for (int x = 0; x < maskTexture.width; x++)
                {
                    int targetX = maskTexture.height - 1 - y;
                    int targetY = x;

                    rotatedPixels[targetY * maskTexture.height + targetX] = flippedPixels[y * maskTexture.width + x];
                }
            }

            maskTexture.SetPixels(rotatedPixels);

            //Apply mask
            maskTexture.Apply();
            TextureScale.Scale(maskTexture, ogDims.Item1, ogDims.Item2);
            byte[] finalData = maskTexture.EncodeToPNG();
            File.WriteAllBytes($"{tmp_path}/{tmpID}", finalData);

            //Dispose all
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(maskTexture);
            RenderTexture.active = null;
            resultRT.Release();
            UnityEngine.Object.DestroyImmediate(resultRT);
            imageTensor.Dispose();
            workerBG.Dispose();

            return "";
        }

    }
}