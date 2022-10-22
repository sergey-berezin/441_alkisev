using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using FacesSimilarity;
using System.Threading;

namespace FaceCompareApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<byte[]> Images;
        public FacesComparator FacesComparator;
        public CancellationTokenSource cts;

        public MainWindow()
        {
            Images = new List<byte[]>();
            FacesComparator = new FacesComparator();
            cts = new CancellationTokenSource();
            InitializeComponent();
            MainGrid.DataContext = this;
        }

        private static TextBlock CreateTextBlock(string distance, string similarity)
        {
            var tb = new TextBlock();
            tb.Inlines.Add(new Run(distance) { Foreground = Brushes.Red });
            tb.Inlines.Add(new Run("\n  " + similarity) { Foreground = Brushes.LightGreen });
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }

        private void LoadImagesClick(object sender, RoutedEventArgs e)
        {
            DoClear();
            var ofd = new OpenFileDialog { Multiselect = true };
            var response = ofd.ShowDialog();
            if (response == true)
            {
                foreach (var path in ofd.FileNames)
                {
                    Images.Add(System.IO.File.ReadAllBytes(path));
                }
            }
            ComparisonData.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            ComparisonData.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            var tb = CreateTextBlock("Distance", "Similarity");
            Grid.SetColumn(tb, 0);
            Grid.SetRow(tb, 0);
            ComparisonData.Children.Add(tb);
            for (int i = 0; i < Images.Count; i++)
            {
                ComparisonData.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                ComparisonData.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                var image = new System.Windows.Controls.Image
                {
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i])
                };
                Grid.SetColumn(image, 0);
                Grid.SetRow(image, i + 1);
                ComparisonData.Children.Add(image);

                image = new System.Windows.Controls.Image
                {
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i])
                };
                Grid.SetColumn(image, i + 1);
                Grid.SetRow(image, 0);
                ComparisonData.Children.Add(image);
            }
        }

        private async void CompareClick(object sender, RoutedEventArgs e)
        {
            if (cts.IsCancellationRequested)
            {
                cts = new CancellationTokenSource();
            }
            var n = Images.Count;
            if (ComparisonData.Children.Count > 2 * n + 1)
            {
                ComparisonData.Children.RemoveRange(2 * n + 1, ComparisonData.Children.Count - 2 * n - 1);
            }
            var progress_step = 100.0 / (n * n);
            ComparisonPB.Value = 0;
            ComparisonPB.Foreground = Brushes.Green;
            for (int i = 0; i < n && !cts.IsCancellationRequested; ++i)
            {
                for (int j = 0; j < n && !cts.IsCancellationRequested; ++j)
                {
                    var v = await FacesComparator.CompareAsync(Images[i], Images[j], cts.Token);
                    var tb = CreateTextBlock(v.Item1.ToString("F4"), v.Item2.ToString("F4"));
                    Grid.SetColumn(tb, i + 1);
                    Grid.SetRow(tb, j + 1);
                    ComparisonData.Children.Add(tb);
                    ComparisonPB.Value += progress_step;
                }
            }
            if (!cts.IsCancellationRequested)
            {
                ComparisonPB.Value = 100;
            }
            else
            {
                ComparisonPB.Foreground = Brushes.Red;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private void ClearClick(object sender, RoutedEventArgs e)
        {
            DoClear();
        }

        private void DoClear()
        {
            ComparisonPB.Value = 0;
            ComparisonData.Children.Clear();
            ComparisonData.RowDefinitions.Clear();
            ComparisonData.ColumnDefinitions.Clear();
            Images.Clear();
        }
    }
}
