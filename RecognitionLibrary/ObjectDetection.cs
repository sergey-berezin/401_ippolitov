﻿using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace RecognitionLibrary
{
    public class ObjectDetection
    {
        const string modelPath = @"C:\Users\Владимир\Downloads\yolov4.onnx";
        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        
        public static void Detect(string directory,
            CancellationTokenSource cts,
            ConcurrentQueue<Tuple<string, YoloV4Result>> detectionResults)
        {
            var filenames = Directory.GetFiles(directory).Select(path => Path.GetFullPath(path)).ToArray();

            var modelResults = new ConcurrentStack<YoloV4Result>();
            MLContext mlContext = new MLContext();

            var pipeline = mlContext.Transforms.ResizeImages(
                inputColumnName: "bitmap",
                outputColumnName: "input_1:0",
                imageWidth: 416, imageHeight: 416,
                resizing: ResizingKind.IsoPad
                ).Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0",
                scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));

            // Fit on empty list to obtain input data schema
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));

            // Create prediction engine


            var sw = new Stopwatch();
            sw.Start();



            var tasks = new Task[filenames.Length];
            for (int i = 0; i < filenames.Length; ++i)
            {
                tasks[i] = Task.Factory.StartNew(pi =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    int file_index = (int)pi;
                    var path = filenames[file_index];
                    var bitmap = new Bitmap(Image.FromFile(path));
                    var predictionEngine = mlContext.Model
                    .CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);
                    var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                    var results = predict.GetResults(classesNames, 0.3f, 0.7f);

                    foreach (var detected in results)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            return;
                        }

                        var resTuple = new Tuple<string, YoloV4Result>(path, detected);
                        detectionResults.Enqueue(resTuple);
                    }
                }, i);
            }


            Task.WaitAll(tasks);
            cts.Cancel();
            sw.Stop();
        }
    }
}
