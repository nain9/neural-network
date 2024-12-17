using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Diagnostics;

namespace NeuralNetwork1
{
    /// <summary>
    /// Тип фигуры
    /// </summary>
    public enum FigureType : byte { First = 0, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth, Ninth, Tenth, Undef };
    
    public class GenerateImage
    {
        /// <summary>
        /// Бинарное представление образа
        /// </summary>
        public bool[,] img = new bool[200, 200];
        
        //  private int margin = 50;
        private Random random = new Random();
        
        private MagicEye magicEye = new MagicEye();
        
        /// <summary>
        /// Текущая сгенерированная фигура
        /// </summary>
        public FigureType currentFigure = FigureType.Undef;

        /// <summary>
        /// Количество классов генерируемых фигур (10 - максимум)
        /// </summary>
        public int FigureCount { get; set; } = 10;

        private string dataSetPath = "..\\..\\dataset";
        
        private int imgSize = 100;
        
        /// <summary>
        /// Очистка образа
        /// </summary>
        public void ClearImage()
        {
            for (int i = 0; i < imgSize; ++i)
            {
                for (int j = 0; j < imgSize; ++j)
                {
                    img[i, j] = false;
                }
            }
        }

        public Sample GenerateFigure(FigureType type = FigureType.Undef, bool processed = false, bool noise = false, int index = 0)
        {
            generate_figure(type, processed, index);
            type = currentFigure;
            
            double noiseProbability = 0.005;
            for (int i = 0; i < imgSize; i++)
            {
                for (int j = 0; j < imgSize; j++)
                {
                    if (noise)
                    {
                        if (random.NextDouble() < noiseProbability)
                        {
                            img[i, j] = true;
                        }
                    }
                }
            }
            
            var input = MakeInput();
            
            return new Sample(input, FigureCount, type);
        }
        
        public void generate_figure(FigureType type = FigureType.Undef, bool processed = false, int index = 0)
        {
            if (type == FigureType.Undef || (int)type >= FigureCount)
                type = (FigureType)random.Next(FigureCount);
            ClearImage();
            currentFigure = type;

            string imagePath;
            if (processed)
            {
                imagePath = Path.Combine(dataSetPath, "processed", ((int)type).ToString(), index + ".jpg");
            }
            else
            {
                int fileIndex = random.Next(20);
                imagePath = Path.Combine(dataSetPath, ((int)type).ToString(), fileIndex + ".jpg");
            }
            
            if (File.Exists(imagePath))
            {
                LoadImage(imagePath, processed);
            }
            else
            {
                throw new FileNotFoundException($"Image not found at path: {imagePath}");
            }
        }
        
        private void LoadImage(string path, bool processed = false)
        {
            Bitmap bitmap = new Bitmap(path);
            if (!processed)
            {
                magicEye.ProcessImage(bitmap);
                bitmap = magicEye.processed;
            }
            for (int i = 0; i < imgSize; i++)
            {
                for (int j = 0; j < imgSize; j++)
                {
                    Color pixel = bitmap.GetPixel(i, j);
                    img[i, j] = pixel.GetBrightness() < 0.5; 
                }
            }
        }

        public Sample LoadFromCamera(Bitmap bitmap)
        {
            for (int i = 0; i < imgSize; i++)
            {
                for (int j = 0; j < imgSize; j++)
                {
                    Color pixel = bitmap.GetPixel(i, j);
                    img[i, j] = pixel.GetBrightness() < 0.5; 
                }
            }
            
            FigureType type = FigureType.Undef;
            
            var input = MakeInput();

            return new Sample(input, FigureCount, type);
        }

        private double[] MakeInput()
        {
            double[] input = new double[603];
            for (int i = 0; i < 603; i++)
            {
                input[i] = 0;
            }
            
            int blackPixelCount = 0;
            int iSum = 0;
            int jSum = 0;
            
            for (int i = 0; i < imgSize; i++)
            {
                int rowChanges = 0; 
                int maxLength = 0;
                int currentLength = 0; 

                for (int j = 1; j < imgSize - 1; j++)
                {
                    if (img[i, j] != img[i, j - 1] && img[i, j] == img[i, j + 1])
                    {
                        rowChanges++;
                    }
                    if (img[i, j])
                    {
                        currentLength++;
                        maxLength = Math.Max(maxLength, currentLength);
                        input[400 + i] += 1;
                        input[500 + j] += 1;
                        blackPixelCount++;
                        jSum += j; 
                        iSum += i;
                    }
                    else
                    {
                        currentLength = 0;
                    }
                }
                input[i] = rowChanges; 
                input[200 + i] = maxLength; 
            }

            for (int j = 0; j < imgSize; j++)
            {
                int colChanges = 0; 
                int maxLength = 0; 
                int currentLength = 0; 

                for (int i = 1; i < imgSize - 1; i++)
                {
                    if (img[i, j] != img[i - 1, j] && img[i, j] == img[i + 1, j])
                    {
                        colChanges++;
                    }
                    if (img[i, j])
                    {
                        currentLength++;
                        maxLength = Math.Max(maxLength, currentLength);
                    }
                    else
                    {
                        currentLength = 0;
                    }
                }

                input[100 + j] = colChanges;
                input[300 + j] = maxLength;
            }
            
            input[600] = blackPixelCount > 0 ? (double)iSum / blackPixelCount : 0;
            input[601] = blackPixelCount > 0 ? (double)jSum / blackPixelCount : 0;
            input[602] = (double)blackPixelCount / (imgSize * imgSize);
            
            
            /*double[] input = new double[603];
            for (int i = 0; i < 603; i++)
            {
                input[i] = 0;
            }
            
            int blackPixelCount = 0;
            int iSum = 0;
            int jSum = 0;
            
            for (int i = 1; i < imgSize - 1; i++)
            {
                int rowChanges = 0;
                for (int j = 1; j < imgSize - 1; j++)
                {
                    if (AreAllNeighborsFalse(i, j))
                    {
                        continue;
                    }
                    if (img[i, j] != img[i, j - 1] && img[i, j] == img[i, j + 1])
                    {
                        rowChanges++;
                    }
                    if (img[i, j])
                    {
                        input[i] += 1;
                        input[100 + j] += 1;
                        blackPixelCount++;
                        jSum += j; 
                        iSum += i;
                    }
                }
                input[200 + i] = rowChanges; 
            }

            for (int j = 1; j < imgSize - 1; j++)
            {
                int colChanges = 0;
                for (int i = 1; i < imgSize - 1; i++)
                {
                    if (AreAllNeighborsFalse(i, j))
                    {
                        continue;
                    }
                    if (img[i, j] != img[i - 1, j] && img[i, j] == img[i + 1, j])
                    {
                        colChanges++;
                    }
                }
                input[300 + j] = colChanges;
            }
            
            input[400] = blackPixelCount > 0 ? (double)iSum / blackPixelCount : 0;
            input[401] = blackPixelCount > 0 ? (double)jSum / blackPixelCount : 0;
            input[402] = (double)blackPixelCount / (imgSize * imgSize);*/
            
            
            
            return input;
        }
        
        bool AreAllNeighborsFalse(int i, int j)
        {
            if (img[i - 1, j] || img[i + 1, j] || img[i, j - 1] || img[i, j + 1] ||
                img[i - 1, j - 1] || img[i - 1, j + 1] || img[i + 1, j - 1] || img[i + 1, j + 1])
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Возвращает битовое изображение для вывода образа
        /// </summary>
        /// <returns></returns>
        public Bitmap GenBitmap()
        {
            Bitmap drawArea = new Bitmap(imgSize, imgSize);
            for (int i = 0; i < imgSize; ++i)
                for (int j = 0; j < imgSize; ++j)
                    if (img[i, j])
                        drawArea.SetPixel(i, j, Color.Black);
                    else
                        drawArea.SetPixel(i, j, Color.White);
            return drawArea;
        }
    }
}
