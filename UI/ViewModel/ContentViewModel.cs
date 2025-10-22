using Common.Message;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace UI.ViewModel
{
    public partial class ContentViewModel : ObservableObject
    {
        public Common.Log LogInstance { get; }

        private readonly SearchObject.MLModel _model;

        [ObservableProperty] private BitmapImage _imageData;
        [ObservableProperty] private string _loadedImage;
        [ObservableProperty] private string _loadedModel;
        [ObservableProperty] private string _dataPath;

        public ContentViewModel(Common.Log log, SearchObject.MLModel model)
        {
            LogInstance = log;
            _model = model;
        }

        [RelayCommand]
        public void LoadImage()
        {
            try
            {
                OpenFileDialog ofd = new();
                ofd.Filter = "ImageFile|*.jpg;*.bmp;*.png;*.jpeg";
                var result = ofd.ShowDialog();
                if (result == null || !result.Value)
                {
                    return;
                }

                ImageData = new BitmapImage();
                ImageData.BeginInit();
                ImageData.UriSource = new(ofd.FileName);
                ImageData.EndInit();

                LoadedImage = ofd.FileName;

                LogInstance.Write($"loaded {ofd.FileName}");
            }
            catch (Exception ex)
            {
                LogInstance.Write($"fail load\n{ex.Message}");
                ImageData = new BitmapImage();
            }
        }

        [RelayCommand]
        public async Task LoadModel()
        {
            try
            {
                OpenFileDialog ofd = new();
                ofd.Filter = "Model|*.mlnet";
                var result = ofd.ShowDialog();
                if (result == null || !result.Value)
                {
                    return;
                }
                LoadedModel = ofd.FileName;

                WeakReferenceMessenger.Default.Send(new BusyMessage(true));
                await _model.LoadModelAsync(ofd.FileName);
                WeakReferenceMessenger.Default.Send(new BusyMessage(false));

                LogInstance.Write($"loaded {ofd.FileName}");
            }
            catch (Exception ex)
            {
                LogInstance.Write($"fail load\n{ex.Message}");
                ImageData = new BitmapImage();
            }
        }

        [RelayCommand]
        public void SetDataPath()
        {
            try
            {
                OpenFileDialog ofd = new();
                ofd.Filter = "json|*.json;";
                var result = ofd.ShowDialog();
                if (result == null || !result.Value)
                {
                    return;
                }
                DataPath = ofd.FileName;
            }
            catch (Exception ex)
            {
                LogInstance.Write($"fail load\n{ex.Message}");
            }
        }

        [RelayCommand]
        public async Task Retrain()
        {
            try
            {
                if (string.IsNullOrEmpty(DataPath))
                {
                    throw new Exception("data path is empty");
                }

                SaveFileDialog sfd = new();
                sfd.Filter = "mlnet|*.mlnet";
                var result = sfd.ShowDialog();
                if (result == null || !result.Value)
                {
                    return;
                }
                await _model.Train(DataPath, sfd.FileName);
                LogInstance.Write($"complete\n{sfd.FileName}");
            }
            catch (Exception ex)
            {
                LogInstance.Write($"fail train\n{ex.Message}");
            }

        }

        [RelayCommand]
        public async Task RunPredict()
        {
            try
            {
                var result = await _model.Predict(LoadedImage);
                var data = File.ReadAllBytes(LoadedImage);

                List<Model.PredictResult> resultList = [];

                for (int i = 0; i < result.PredictedBoundingBoxes.Length; i += 4)
                {
                    Model.PredictResult item = new();
                    item.Score = result.Score[i / 4];
                    item.Rect = new(
                        (int)result.PredictedBoundingBoxes[i],
                        (int)result.PredictedBoundingBoxes[i + 1],
                        (int)result.PredictedBoundingBoxes[i + 2] - (int)result.PredictedBoundingBoxes[i],
                        (int)result.PredictedBoundingBoxes[i + 3] - (int)result.PredictedBoundingBoxes[i + 1]);

                    resultList.Add(item);
                }

                LogInstance.Write($"SearchCount={resultList.Count}");

                resultList = resultList.Take(resultList.Count > 4 ? 4 : resultList.Count).ToList();

                using var ms = new MemoryStream(data);
                using var image = Image.FromStream(ms);
                using var bitmap = new Bitmap(image);
                using var graphics = Graphics.FromImage(bitmap);
                using var pen = new Pen(Color.Green, 2);
                using var output = new MemoryStream();

                foreach (var v in resultList)
                {
                    graphics.DrawRectangle(pen, v.Rect);
                    graphics.DrawString(v.Score.ToString("f2"), new("Arial", 20), Brushes.Green, v.Rect.X, v.Rect.Y);
                }
                bitmap.Save(output, ImageFormat.Jpeg); // 또는 JPEG 등 원하는 포맷
                data = output.ToArray();

                ImageData = BytesToBitmap(data);
            }
            catch (Exception ex)
            {
                LogInstance.Write($"fail predict\n{ex.Message}");
            }
        }

        private static BitmapImage BytesToBitmap(byte[] bytes)
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = new MemoryStream(bytes);
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }
}
