using Microsoft.Win32;
using SignalProcessing.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace SignalGenerator.App
{
    public partial class MainWindow : Window
    {
        private readonly List<SingleChannelSignal> signalList = new List<SingleChannelSignal>();
        private readonly List<RangeAnnotation> annotations = new List<RangeAnnotation>();
        private float annotationMin = -1;
        private float annotationMax = 1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void addSignal(SingleChannelSignal signal)
        {
            signalList.Add(signal);
            WpfPlot1.Plot.AddSignal(signal.Value, signal.Freq);
            WpfPlot1.Plot.AxisAuto();
            WpfPlot1.Refresh();
        }

        private float ReadFloat(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        private void RunAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Button_Click_Sin(object sender, RoutedEventArgs e) => RunAction(() =>
            addSignal(Tools.Sin(ReadFloat(len_textBox.Text), ReadFloat(freq_textBox.Text), ReadFloat(period_textBox.Text), ReadFloat(phase_textBox.Text), ReadFloat(scale_textBox.Text))));

        private void Button_Click_Line(object sender, RoutedEventArgs e) => RunAction(() =>
            addSignal(Tools.Line(ReadFloat(len_textBox.Text), ReadFloat(freq_textBox.Text), ReadFloat(k_textBox.Text), ReadFloat(b_textBox.Text))));

        private void Button_Click_Noise(object sender, RoutedEventArgs e) => RunAction(() =>
            addSignal(Tools.Noise(ReadFloat(len_textBox.Text), ReadFloat(freq_textBox.Text), ReadFloat(Amplitude_textBox.Text))));

        private void Button_Clear(object sender, RoutedEventArgs e)
        {
            signalList.Clear();
            annotations.Clear();
            WpfPlot1.Plot.Clear();
            WpfPlot1.Refresh();
        }

        private void Button_Write(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            if (signalList.Count == 0)
                throw new InvalidOperationException("Нет сигналов для сохранения.");
            SaveFileDialog dialog = new SaveFileDialog { Filter = "CSV-файл (*.csv)|*.csv", FileName = "MySignal", DefaultExt = "csv" };
            if (dialog.ShowDialog() == true)
                SingleChannelSignalWriter.Write(signalList.Last(), dialog.FileName);
        });

        private void Button_Load(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "CSV-файл (*.csv)|*.csv", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog() == true)
                addSignal(SingleChannelSignalReader.Load(dialog.FileName));
        });

        private void Button_Click_Remove(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0)
                return;
            signalList.RemoveAt(signalList.Count - 1);
            Redraw();
        }

        private void Button_Click_Summ(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal result = Tools.Summ(signalList);
            if (result == null)
                throw new InvalidOperationException("Для суммирования нужны сигналы с одинаковой частотой дискретизации.");
            addSignal(result);
        });

        private void Button_Click_Concat(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal result = Tools.Concat(signalList);
            if (result == null)
                throw new InvalidOperationException("Для склейки нужны сигналы с одинаковой частотой дискретизации.");
            addSignal(result);
        });

        private int GetWindowSize(bool requireOdd)
        {
            if (signalList.Count == 0)
                throw new InvalidOperationException("Сначала создайте или загрузите сигнал.");
            float seconds = ReadFloat(Window_textBox.Text);
            if (seconds <= 0)
                throw new ArgumentException("Длина окна должна быть положительной.");
            int size = Math.Max(1, (int)Math.Round(seconds * signalList.Last().Freq, MidpointRounding.AwayFromZero));
            if (requireOdd && size % 2 == 0)
                size++;
            return size;
        }

        private void Button_Click_Offline(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            addSignal(new SingleChannelSignal(source.Freq, OfflineFilter.Filter(source.Value, GetWindowSize(true))));
        });

        private void Button_Click_Online(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            int windowSize = GetWindowSize(false);
            OnlineFilter filter = new OnlineFilter(windowSize);
            List<float> result = new List<float>();
            const int chunkSize = 7;
            for (int offset = 0; offset < source.Value.Length; offset += chunkSize)
                result.AddRange(filter.Filter(source.Value.Skip(offset).Take(chunkSize).ToArray()));
            addSignal(new SingleChannelSignal(source.Freq, result.ToArray()));
        });

        private SingleChannelSignal GetLastSignal()
        {
            if (signalList.Count == 0)
                throw new InvalidOperationException("Сначала создайте или загрузите сигнал.");
            return signalList.Last();
        }

        private void Button_AnalyzeRange(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            annotationMin = ReadFloat(downCut_textBox.Text);
            annotationMax = ReadFloat(upCut_textBox.Text);
            int duration = 1;
            annotations.Clear();
            SingleChannelRangeAnalyzer analyzer = new SingleChannelRangeAnalyzer(annotationMin, annotationMax, duration);
            analyzer.OutOfRange += (s, args) => annotations.Add(new RangeAnnotation(args.StartIndex, args.EndIndex));
            analyzer.Analyze(source.Value);
            Redraw();
            MessageBox.Show("Найдено фрагментов: " + annotations.Count, "Анализ завершён");
        });

        private void Button_SaveAnnotations(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SaveFileDialog dialog = new SaveFileDialog { Filter = "CSV-файл (*.csv)|*.csv", FileName = "Annotations", DefaultExt = "csv" };
            if (dialog.ShowDialog() != true)
                return;
            using (StreamWriter writer = new StreamWriter(dialog.FileName))
            {
                writer.WriteLine(annotationMin.ToString(CultureInfo.InvariantCulture) + ";" + annotationMax.ToString(CultureInfo.InvariantCulture));
                foreach (RangeAnnotation item in annotations)
                    writer.WriteLine(item.StartIndex + ";" + item.EndIndex);
            }
        });

        private void Button_LoadAnnotations(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "CSV-файл (*.csv)|*.csv", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog() != true)
                return;
            string[] lines = File.ReadAllLines(dialog.FileName);
            if (lines.Length == 0)
                throw new InvalidDataException("Файл разметки пуст.");
            string[] limits = lines[0].Split(';');
            annotationMin = float.Parse(limits[0], CultureInfo.InvariantCulture);
            annotationMax = float.Parse(limits[1], CultureInfo.InvariantCulture);
            annotations.Clear();
            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string[] values = line.Split(';');
                annotations.Add(new RangeAnnotation(int.Parse(values[0]), int.Parse(values[1])));
            }
            Redraw();
        });

        private void Redraw()
        {
            WpfPlot1.Plot.Clear();
            foreach (SingleChannelSignal signal in signalList)
                WpfPlot1.Plot.AddSignal(signal.Value, signal.Freq);
            if (signalList.Count > 0)
            {
                float freq = signalList.Last().Freq;
                foreach (RangeAnnotation item in annotations)
                {
                    float start = item.StartIndex / freq;
                    float end = item.EndIndex / freq;
                    WpfPlot1.Plot.AddLine(start, annotationMin, start, annotationMax);
                    WpfPlot1.Plot.AddLine(end, annotationMin, end, annotationMax);
                }
            }
            WpfPlot1.Plot.AxisAuto();
            WpfPlot1.Refresh();
        }

        private void Button_Click_CutUp(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal signal = GetLastSignal();
            ThresholdCutter cutter = new ThresholdCutter();
            cutter.SetSignal(signal);
            cutter.CutUp(ReadFloat(upCut_textBox.Text));
            Redraw();
        });

        private void Button_Click_CutDown(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal signal = GetLastSignal();
            ThresholdCutter cutter = new ThresholdCutter();
            cutter.SetSignal(signal);
            cutter.CutDown(ReadFloat(downCut_textBox.Text));
            Redraw();
        });

        private void Button_Click_CutUpAndDown(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal signal = GetLastSignal();
            ThresholdCutter cutter = new ThresholdCutter();
            cutter.SetSignal(signal);
            cutter.CutUp(ReadFloat(upCut_textBox.Text));
            cutter.CutDown(ReadFloat(downCut_textBox.Text));
            Redraw();
        });
    }
}

