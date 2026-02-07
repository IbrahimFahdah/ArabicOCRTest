using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Tesseract;

namespace ArabicOCR;

public partial class MainWindow : Window
{
    private string? _loadedFilePath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnLoadImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All Files|*.*",
            Title = "Select an Arabic document image"
        };

        if (dlg.ShowDialog() == true)
        {
            LoadImage(dlg.FileName);
        }
    }

    private void BtnLoadPdf_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "PDF loading requires an additional library (e.g. PdfiumViewer or Docnet).\n\n" +
            "For now, please convert your PDF pages to images first,\n" +
            "then use 'Load Image' to load them.",
            "PDF Support", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadImage(string filePath)
    {
        _loadedFilePath = filePath;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        ImgPreview.Source = bitmap;
        TxtImagePlaceholder.Visibility = Visibility.Collapsed;
        BtnExtract.IsEnabled = true;

        TxtStatus.Text = $"Loaded: {Path.GetFileName(filePath)}";
        TxtResults.Text = "";
        BtnCopy.IsEnabled = false;
        BarConfidence.Value = 0;
        TxtConfidence.Text = "";
    }

    private async void BtnExtract_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedFilePath == null) return;

        BtnExtract.IsEnabled = false;
        TxtStatus.Text = "Extracting text...";
        TxtResults.Text = "";

        var filePath = _loadedFilePath;
        var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        var (text, confidence) = await Task.Run(() => ExtractText(filePath, tessDataPath));

        TxtResults.Text = text;
        BarConfidence.Value = confidence;
        TxtConfidence.Text = $"{confidence:F1}%";
        BtnCopy.IsEnabled = !string.IsNullOrWhiteSpace(text);
        BtnExtract.IsEnabled = true;
        TxtStatus.Text = string.IsNullOrWhiteSpace(text)
            ? "No text detected."
            : $"Done — {text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length} segments extracted.";
    }

    private static (string Text, float Confidence) ExtractText(string imagePath, string tessDataPath)
    {
        try
        {
            using var engine = new TesseractEngine(tessDataPath, "ara", EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);

            float confidence = page.GetMeanConfidence() * 100;
            var fullText = page.GetText();

            // Build segmented output using layout iterator
            var segments = new System.Text.StringBuilder();
            using var iter = page.GetIterator();
            iter.Begin();

            int blockIndex = 1;
            do
            {
                if (iter.IsAtBeginningOf(PageIteratorLevel.Block))
                {
                    var blockText = iter.GetText(PageIteratorLevel.Block)?.Trim();
                    if (!string.IsNullOrWhiteSpace(blockText))
                    {
                        segments.AppendLine($"═══ Segment {blockIndex} ═══");
                        segments.AppendLine(blockText);
                        segments.AppendLine();
                        blockIndex++;
                    }
                }
            } while (iter.Next(PageIteratorLevel.Block));

            var result = segments.Length > 0 ? segments.ToString() : fullText;
            return (result, confidence);
        }
        catch (Exception ex)
        {
            return ($"Error: {ex.Message}", 0);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtResults.Text))
        {
            Clipboard.SetText(TxtResults.Text);
            TxtStatus.Text = "Text copied to clipboard.";
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _loadedFilePath = null;
        ImgPreview.Source = null;
        TxtImagePlaceholder.Visibility = Visibility.Visible;
        TxtResults.Text = "";
        BtnExtract.IsEnabled = false;
        BtnCopy.IsEnabled = false;
        BarConfidence.Value = 0;
        TxtConfidence.Text = "";
        TxtStatus.Text = "";
    }
}
