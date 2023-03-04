using AspNetCoreHero.ToastNotification.Abstractions;
using IronOcr;
using IronSoftware.Drawing;
using Microsoft.AspNetCore.Mvc;
using OCR.Models;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.Versioning;
using static IronOcr.OcrResult;
using Color = System.Drawing.Color;

namespace OCR.Controllers
{
    public class MainController : Controller
    {
        private INotyfService _notifyService { get; }

        public MainController(INotyfService notifyService)
        {
            _notifyService = notifyService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SearchWord()
        {
            return View();
        }

        [HttpPost]
        [SupportedOSPlatform("windows")]
        public IActionResult SearchWord(string fileRoute, string searchedWord)
        {
            try
            {
                if (!string.IsNullOrEmpty(fileRoute) && !string.IsNullOrEmpty(searchedWord))
                {
                    if (!System.IO.File.Exists(fileRoute))
                    {
                        _notifyService.Error("The full path to the file does not exist.");
                        return View();
                    }

                    List<Image> images = new();
                    IronTesseract ocr = new IronTesseract();

                    ocr.Configuration.ReadBarCodes = true;

                    using (OcrInput input = new OcrInput(fileRoute))
                    {
                        OcrResult result = ocr.Read(input);
                        foreach (Page page in result.Pages)
                        {
                            List<Rectangle> rectanglesFull = new();
                            List<Rectangle> rectanglesPartial = new();

                            AnyBitmap pageImage = page.ToBitmap(input);

                            foreach (Paragraph paragraph in page.Paragraphs)
                                foreach (Line line in paragraph.Lines)
                                    foreach (Word word in line.Words)
                                        if (word.Text.ToLower() == searchedWord.ToLower())
                                            rectanglesFull.Add(new(word.X, word.Y, word.Width, word.Height));
                                        else if (word.Text.ToLower().Contains(searchedWord.ToLower()))
                                            rectanglesPartial.Add(new(word.X, word.Y, word.Width, word.Height));

                            Pen redPen = new(Color.Red, 3);
                            Pen bluePen = new(Color.Blue, 3);

                            Image image = pageImage.Clone();

                            using (Graphics graphics = Graphics.FromImage(image))
                            {
                                foreach (Rectangle rect in rectanglesFull)
                                    graphics.DrawRectangle(bluePen, rect);

                                foreach (Rectangle rect in rectanglesPartial)
                                    graphics.DrawRectangle(redPen, rect);
                            }
                                
                            images.Add(image);
                        }
                    }

                    string zipName = $"Processed-{DateTime.Now.ToString("yyyy_MM_dd-HH_mm_ss")}.zip";

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (ZipArchive zip = new (ms, ZipArchiveMode.Create, true))
                            for (int i = 0; i < images.Count; i++)
                            {
                                ZipArchiveEntry entry = zip.CreateEntry($"output-{i}.png");

                                using (MemoryStream imageStream = new())
                                {
                                    images[i].Save(imageStream, ImageFormat.Png);
                                    imageStream.Position = 0;

                                    using (var entryStream = entry.Open())
                                        imageStream.CopyTo(entryStream);
                                }
                            }

                        return File(ms.ToArray(), "application/zip", zipName);
                    }
                }
                else
                    _notifyService.Warning("You must fill in the form.");
            }
            catch(Exception ex)
            {
                _notifyService.Error("Error occurred while searching for a word in a text");
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}