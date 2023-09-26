using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;
using UnityEditor;
using SSL.Data.Utils;
using System.IO;
using System;

namespace SSL.Data
{
    public class Inference
    {
        NNModel _modelAssetEncoder, _modelAssetBottleneck, _modelAssetDecoder;
        Model _modelEncoder, _modelBottleneck, _modelDecoder;
        IWorker _workerEncoder, _workerBottleneck, _workerDecoder;

        System.Random _random;
        float _sizeScale = 15f;

        public Inference()
        {
            _modelAssetEncoder = (NNModel)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/ONNX/encoder_v3.onnx", typeof(NNModel));
            _modelEncoder = ModelLoader.Load(_modelAssetEncoder);
            _workerEncoder = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, _modelEncoder);

            _modelAssetBottleneck = (NNModel)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/ONNX/bottleneck_v3.onnx", typeof(NNModel));
            _modelBottleneck = ModelLoader.Load(_modelAssetBottleneck);
            _workerBottleneck = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, _modelBottleneck);

            _modelAssetDecoder = (NNModel)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/ONNX/decoder_v3.onnx", typeof(NNModel));
            _modelDecoder = ModelLoader.Load(_modelAssetDecoder);
            _workerDecoder = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, _modelDecoder);


            _random = new System.Random();
        }

        //Image to Model
        public GAGenData Img2Model(string path)
        {
            Texture2D image = LoadImage(path, out (int, int) ogDims);
            if (image == null)
                return null;
            //Tensor input = new Tensor(image);
            float[,,,] imageData = LoadChannelAsFloatArray(image);
            Tensor input = new Tensor(1, 64, 64, 1, imageData);
            var output = ReconstructInference(input);
            input.Dispose();
            return output;
        }

        //Latent generation
        public GAGenData Rand2Model()
        {
            Tensor input = new Tensor(1, 512);
            var mean = 0f;
            var stdDev = 1f;

            for (int i = 0; i < input.length; i++)
            {
                double u1 = 1.0 - _random.NextDouble(); //uniform(0,1] random doubles
                double u2 = 1.0 - _random.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                             Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
                double randNormal =
                             mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)

                input[i] = (float)randNormal;
            }
            var output = LatentInference(input);
            input.Dispose();
            return output;
        }

        //Latent interpolation
        public GAGenData Interp(string pathA, string pathB, float t)
        {
            Texture2D imageA = LoadImage(pathA, out (int, int) ogDimsA);
            Texture2D imageB = LoadImage(pathB, out (int, int) ogDimsB);
            float[,,,] imageDataA = LoadChannelAsFloatArray(imageA);
            float[,,,] imageDataB = LoadChannelAsFloatArray(imageB);
            Tensor inputA = new Tensor(1, 64, 64, 1, imageDataA);
            Tensor inputB = new Tensor(1, 64, 64, 1, imageDataB);
            Tensor latentA = EncodeInference(inputA);
            Tensor latentB = EncodeInference(inputB);
            Tensor latentLerp = new Tensor(new int[] { 1, 512 });
            for (int i = 0; i < latentLerp.length; i++)
            {
                latentLerp[i] = Mathf.Lerp(latentA[i], latentB[i], t);
            }
            var output = LatentInference(latentLerp);
            inputA.Dispose();
            inputB.Dispose();
            latentA.Dispose();
            latentB.Dispose();
            latentLerp.Dispose();
            return output;
        }

        public static Texture2D LoadImage(string path, out (int, int) originalDims, int scale = 64)
        {
            Texture2D texture = null;
            byte[] data;

            if (File.Exists(path))
            {
                data = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, 1, false);
                bool imageloaded = texture.LoadImage(data);
                originalDims = (texture.width, texture.height);
                TextureScale.Scale(texture, scale, scale);
                //var pixels = texture.GetPixels(0);
                //for (int i = 0; i < pixels.Length; i++)
                //{
                //    Debug.Log(pixels[i]);
                //}
                //Debug.Log($"Texture readable: {texture.isReadable}, Image loaded: {imageloaded}, Mipmaps: {texture.mipmapCount}");
            }
            else
                throw new Exception($"Compatible file was not selected: {path}");
            return texture;
        }

        public static float[,,,] LoadChannelAsFloatArray(Texture2D texture, int channels = 1)
        {
            float[,,,] data = new float[1, texture.width, texture.height, channels];
            var pixels = texture.GetPixels(0);
            for (int i = 0; i < texture.width; i++)
            {
                for (int j = 0; j < texture.height; j++)
                {
                    //data[0, i, j, 0] = texture.GetPixel(i, (texture.height - 1) - j).a;
                    data[0, i, j, 0] = pixels[i + (((texture.height - 1) - j) * texture.width)].r;
                    //Debug.Log(data[0, i, j, 0]);
                }
            }
            return data;
        }

        GAGenData ReconstructInference(Tensor input)
        {
            _workerEncoder.Execute(input);
            Tensor result = _workerEncoder.PeekOutput();
            //FileStream fs = new FileStream("test.txt", FileMode.OpenOrCreate);
            //using (StreamWriter sw = new StreamWriter(fs))
            //{
            //    sw.Write(result.DataToString());
            //}
            _workerBottleneck.Execute(result);
            result = _workerBottleneck.PeekOutput();
            _workerDecoder.Execute(result);
            result = _workerDecoder.PeekOutput();

            result.Flatten();

            
            var output = Tensor2Graph(result);
            //_workerEncoder.Dispose();
            //_workerBottleneck.Dispose();
            //_workerDecoder.Dispose();
            return output;
        }

        GAGenData LatentInference(Tensor input)
        {
            //Debug.Log(input.shape);
            _workerDecoder.Execute(input);
            Tensor result = _workerDecoder.PeekOutput();
            result.Flatten();
            var output = Tensor2Graph(result);
            //_workerDecoder.Dispose();
            return output;
        }
        Tensor EncodeInference(Tensor input)
        {
            _workerEncoder.Execute(input);
            Tensor result = _workerEncoder.PeekOutput();
            _workerBottleneck.Execute(result);
            result = _workerBottleneck.PeekOutput();
            var output = result.DeepCopy();
            //_workerEncoder.Dispose();
            //_workerBottleneck.Dispose();
            return output;
        }

        GAGenData GraphVariator(GAGenData data)
        {
            return data;
        }


        GAGenData Tensor2Graph(Tensor result)
        {
            List<GAGenNodeData> newNodes = new List<GAGenNodeData>();
            string lastGuid = string.Empty;
            for (int i = 0; i < 12; i++)
            {
                //Debug.Log(i * 6 + 5);
                //Debug.Log($"x:{result[i * 6]}");
                //Debug.Log($"y:{result[(i * 6) + 1]}");
                GAGenNodeData newNodeData = new GAGenNodeData();
                newNodeData.ID = Guid.NewGuid().ToString();
                newNodeData.NodeType = Graph.NodeType.Segment;
                NodeSetting newSettings = new NodeSetting();
                newSettings.parameters.size = new Vector3(
                    ((result[i * 6]       + 1) / 2) * _sizeScale, 
                    ((result[(i * 6) + 1] + 1) / 2) * _sizeScale, 
                    1f);

                float sizeMagnitude = Mathf.Sqrt((newSettings.parameters.size.x * newSettings.parameters.size.x) + (newSettings.parameters.size.y * newSettings.parameters.size.y));
                if (sizeMagnitude < 1f)
                    break; //Remove vestigial nodes

                float curveScale = 0.3f;

                newSettings.parameters.curveParams.controlPoint = new Vector2((result[(i * 6) + 2] * _sizeScale), 0f) * curveScale;
                newSettings.parameters.curveParams.tipOffset =    new Vector2((result[(i * 6) + 3] * _sizeScale), 0f) * curveScale;
                newSettings.parameters.nLoops = 5;
                newSettings.parameters.relativeBackwardTaper = result[(i * 6) + 4];
                newSettings.parameters.relativeForwardTaper = result[(i * 6) + 5];
                newSettings.parameters.curveNotAffectNormal = true;
                newNodeData.Settings = newSettings;
                if (i != 0)
                {
                    newNodeData.InGoingConnections = new List<ConnectionData>() { new ConnectionData(lastGuid, 0, 0, newNodeData.ID, Graph.GAPortType.Mesh) };
                    newNodeData.Position = new Vector2(i * 400f, 0f);
                }
                lastGuid = newNodeData.ID;
                newNodes.Add(newNodeData);
            }
            result.Dispose();
            GAGenData output = ScriptableObject.CreateInstance<GAGenData>();
            output.Nodes = newNodes;
            //Debug.Log(output.Nodes[0].Settings.parameters.curveNotAffectNormal);
            return output;
        }

        Tensor Graph2Tensor(GAGenData data)
        {
            Tensor newTensor = new Tensor(new int[] { 1, 72 });
            var nodes = data.Nodes;
            for (int i = 0; i < 12; i++)
            {
                var currentParams = nodes[i].Settings.parameters;
                newTensor[i * 12] = currentParams.size.x;
                newTensor[i * 12 + 1] = currentParams.size.y;
                newTensor[i * 12 + 2] = currentParams.curveParams.controlPoint.x;
                newTensor[i * 12 + 3] = currentParams.curveParams.tipOffset.x;
                newTensor[i * 12 + 4] = currentParams.relativeBackwardTaper;
                newTensor[i * 12 + 5] = currentParams.relativeForwardTaper;
            }
            return newTensor;
        }
    }
}
