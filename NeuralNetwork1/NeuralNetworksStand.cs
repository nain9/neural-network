using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Accord.Math;
using AForge.WindowsForms;

namespace NeuralNetwork1
{
    public partial class NeuralNetworksStand : Form
    {
        /// <summary>
        /// Генератор изображений (образов)
        /// </summary>
        GenerateImage generator = new GenerateImage();
        
        CameraForm cameraForm;
        
        private string datasetProcessedPath = "..\\..\\dataset\\processed";

        private Dictionary<FigureType, string> classesMap = new Dictionary<FigureType, string>()
        {
            { FigureType.First, "Машинная стирка при 30 градусах" },
            { FigureType.Second, "Аккуратная стирка при 30 градусах" },
            { FigureType.Third, "Вы можете использовать хлор" },
            { FigureType.Fourth, "Глажка при температуре до 110 градусов" },
            { FigureType.Fifth, "Не гладить" },
            { FigureType.Sixth, "Сушить" },
            { FigureType.Seventh, "Может быть высушено в духовке" },
            { FigureType.Eighth, "Не сушить в стиральной машине" },
            { FigureType.Ninth, "Вертикальная сушка" },
            { FigureType.Tenth, "Не мойте" },
            { FigureType.Undef, "Это с камеры, я таких картинок не знаю" },
        };

        /// <summary>
        /// Текущая выбранная через селектор нейросеть
        /// </summary>
        public BaseNetwork Net
        {
            get
            {
                var selectedItem = (string) netTypeBox.SelectedItem;
                if (!networksCache.ContainsKey(selectedItem))
                    networksCache.Add(selectedItem, CreateNetwork(selectedItem));

                return networksCache[selectedItem];
            }
        }

        private readonly Dictionary<string, Func<int[], BaseNetwork>> networksFabric;
        private Dictionary<string, BaseNetwork> networksCache = new Dictionary<string, BaseNetwork>();

        /// <summary>
        /// Конструктор формы стенда для работы с сетями
        /// </summary>
        /// <param name="networksFabric">Словарь функций, создающих сети с заданной структурой</param>
        public NeuralNetworksStand(Dictionary<string, Func<int[], BaseNetwork>> networksFabric)
        {
            InitializeComponent();
            this.networksFabric = networksFabric;
            netTypeBox.Items.AddRange(this.networksFabric.Keys.Select(s => (object) s).ToArray());
            netTypeBox.SelectedIndex = 1;
            generator.FigureCount = (int) classCounter.Value;
            button3_Click(this, null);
            pictureBox1.Image = Properties.Resources.Title;
        }

        public void UpdateLearningInfo(double progress, double error, TimeSpan elapsedTime)
        {
            if (progressBar1.InvokeRequired)
            {
                progressBar1.Invoke(new TrainProgressHandler(UpdateLearningInfo), progress, error, elapsedTime);
                return;
            }

            StatusLabel.Text = "Ошибка: " + error;
            int progressPercent = (int) Math.Round(progress * 100);
            progressPercent = Math.Min(100, Math.Max(0, progressPercent));
            elapsedTimeLabel.Text = "Затраченное время : " + elapsedTime.Duration().ToString(@"hh\:mm\:ss\:ff");
            progressBar1.Value = progressPercent;
        }

        private void set_result(Sample figure)
        {
            label1.ForeColor = figure.Correct() ? Color.Green : Color.Red;
            label1.Text = "Распознано : " + classesMap[figure.recognizedClass] + "\nРеальный : " + classesMap[figure.actualClass];

            label8.Text = string.Join("\n", figure.Output.Select(d =>  Math.Round(d, 3).ToString(CultureInfo.InvariantCulture)));
            pictureBox1.Image = generator.GenBitmap();
            pictureBox1.Invalidate();
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            Sample fig = generator.GenerateFigure(noise: true);

            Net.Predict(fig);

            set_result(fig);
        }

        private async Task<double> train_networkAsync(int training_size, int epoches, double acceptable_error,
            bool parallel = true)
        {
            //  Выключаем всё ненужное
            label1.Text = "Выполняется обучение...";
            label1.ForeColor = Color.Red;
            groupBox1.Enabled = false;
            pictureBox1.Enabled = false;
            trainOneButton.Enabled = false;
            buttonCamera.Enabled = false;
            buttonDataSet.Enabled = false;

            //  Создаём новую обучающую выборку
            SamplesSet samples = new SamplesSet();

            if (checkBoxNewDataset.Checked)
                button1_Click_1(null, null);

            for (int i = 0; i < classCounter.Value; i++)
            {
                for (int j = 0; j < (int)(training_size / classCounter.Value); j++)
                {
                    samples.AddSample(generator.GenerateFigure((FigureType)i, true, index: j));
                }
            }
            
            samples.samples.Shuffle();
            
            try
            {
                //  Обучение запускаем асинхронно, чтобы не блокировать форму
                var curNet = Net;
                double f = await Task.Run(() => curNet.TrainOnDataSet(samples, epoches, acceptable_error, parallel));

                label1.Text = "Щелкните на картинку для теста нового образа";
                label1.ForeColor = Color.Green;
                groupBox1.Enabled = true;
                pictureBox1.Enabled = true;
                trainOneButton.Enabled = true;
                buttonCamera.Enabled = true;
                buttonDataSet.Enabled = true;
                StatusLabel.Text = "Ошибка: " + f;
                StatusLabel.ForeColor = Color.Green;
                return f;
            }
            catch (Exception e)
            {
                label1.Text = $"Исключение: {e.Message}";
            }

            return 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            train_networkAsync((int) TrainingSizeCounter.Value, (int) EpochesCounter.Value,
                (100 - AccuracyCounter.Value) / 100.0, parallelCheckBox.Checked);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Enabled = false;
            //  Тут просто тестирование новой выборки
            //  Создаём новую обучающую выборку
            SamplesSet samples = new SamplesSet();

            /*if (checkBoxNewDataset.Checked)
                button1_Click_1(sender, e);*/
            
            for (int i = 0; i < /*(int) TrainingSizeCounter.Value / 10*/ 300; i++)
                samples.AddSample(generator.GenerateFigure(noise: false));

            double accuracy = samples.TestNeuralNetwork(Net);

            StatusLabel.Text = $"Точность на тестовой выборке : {accuracy * 100,5:F2}%";
            StatusLabel.ForeColor = accuracy * 100 >= AccuracyCounter.Value ? Color.Green : Color.Red;

            Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //  Проверяем корректность задания структуры сети
            int[] structure = CurrentNetworkStructure();
            if (structure.Length < 2 /*|| structure[0] != 603*/ ||
                structure[structure.Length - 1] != generator.FigureCount)
            {
                MessageBox.Show(
                    $"В сети должно быть более двух слоёв, первый слой должен содержать 400 нейронов, последний - ${generator.FigureCount}",
                    "Ошибка", MessageBoxButtons.OK);
                return;
            }

            // Чистим старые подписки сетей
            foreach (var network in networksCache.Values)
                network.TrainProgress -= UpdateLearningInfo;
            // Пересоздаём все сети с новой структурой
            networksCache = networksCache.ToDictionary(oldNet => oldNet.Key, oldNet => CreateNetwork(oldNet.Key));
        }

        private int[] CurrentNetworkStructure()
        {
            return netStructureBox.Text.Split(';').Select(int.Parse).ToArray();
        }

        private void classCounter_ValueChanged(object sender, EventArgs e)
        {
            generator.FigureCount = (int) classCounter.Value;
            var vals = netStructureBox.Text.Split(';');
            if (!int.TryParse(vals.Last(), out _)) return;
            vals[vals.Length - 1] = classCounter.Value.ToString();
            netStructureBox.Text = vals.Aggregate((partialPhrase, word) => $"{partialPhrase};{word}");
        }

        private void btnTrainOne_Click(object sender, EventArgs e)
        {
            if (Net == null) return;
            Sample fig = generator.GenerateFigure();
            pictureBox1.Image = generator.GenBitmap();
            pictureBox1.Invalidate();
            Net.Train(fig, 0.00005, parallelCheckBox.Checked);
            set_result(fig);
        }

        private BaseNetwork CreateNetwork(string networkName)
        {
            var network = networksFabric[networkName](CurrentNetworkStructure());
            network.TrainProgress += UpdateLearningInfo;
            return network;
        }

        private void recreateNetButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Заново пересоздаёт сеть с указанными параметрами";
        }

        private void netTrainButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Обучить нейросеть с указанными параметрами";
        }

        private void testNetButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Тестировать нейросеть на тестовой выборке такого же размера";
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                string subFolderPath = Path.Combine(datasetProcessedPath, i.ToString());
                Directory.CreateDirectory(subFolderPath); 
                for (int j = 0; j < TrainingSizeCounter.Value; j++)
                {
                    generator.GenerateFigure((FigureType)i, noise: true);
                    string filePath = Path.Combine(subFolderPath, j + ".jpg");
                    generator.GenBitmap().Save(filePath);
                }
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            if (cameraForm == null || cameraForm.IsDisposed)
            {
                cameraForm = new CameraForm(this);
                cameraForm.Show();
            }
        }

        public void SetImageFromCamera(Bitmap image)
        {
            Sample fig = generator.LoadFromCamera(image);

            Net.Predict(fig);

            set_result(fig);
        }
    }
}