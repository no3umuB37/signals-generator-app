using Microsoft.Win32;
using SignalProcessing.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            signalType_comboBox.ItemsSource = Enum.GetValues(typeof(SignalType));
            signalType_comboBox.SelectedIndex = 0;
        }

        private void addSignal(SingleChannelSignal signal)
        {
            signalList.Add(signal);
            WpfPlot1.Plot.AddSignal(signal.Data, signal.Frequency);
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

        // Читает число из поля ввода. Если текст не является числом,
        // показывает сообщение и возвращает false.
        private bool TryReadFloat(TextBox textBox, string fieldName, out float value)
        {
            if (!float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                MessageBox.Show("Поле «" + fieldName + "» должно содержать число.",
                    "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private SignalType GetSelectedSignalType()
        {
            return (SignalType)signalType_comboBox.SelectedItem;
        }

        // Переключает видимость панелей параметров: видна панель выбранного типа.
        private void SignalType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sinePanel == null)
                return;

            sinePanel.Visibility = Visibility.Collapsed;
            linePanel.Visibility = Visibility.Collapsed;
            noisePanel.Visibility = Visibility.Collapsed;
            squarePanel.Visibility = Visibility.Collapsed;
            sawPanel.Visibility = Visibility.Collapsed;
            trianglePanel.Visibility = Visibility.Collapsed;
            constantPanel.Visibility = Visibility.Collapsed;

            switch (GetSelectedSignalType())
            {
                case SignalType.Sine:
                    sinePanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Line:
                    linePanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Noise:
                    noisePanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Square:
                    squarePanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Saw:
                    sawPanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Triangle:
                    trianglePanel.Visibility = Visibility.Visible;
                    break;
                case SignalType.Constant:
                    constantPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        // Собирает объект параметров по выбранному типу сигнала.
        // Возвращает null, если ввод некорректен.
        private GeneratorParameters ReadParameters(SignalType type)
        {
            switch (type)
            {
                case SignalType.Sine:
                    {
                        float period, phase, amplitude;
                        if (!TryReadFloat(sinePeriod_textBox, "Период", out period))
                            return null;
                        if (!TryReadFloat(sinePhase_textBox, "Фаза", out phase))
                            return null;
                        if (!TryReadFloat(sineAmplitude_textBox, "Амплитуда", out amplitude))
                            return null;
                        return new SineParameters { Period = period, Phase = phase, Amplitude = amplitude };
                    }

                case SignalType.Line:
                    {
                        float k, b;
                        if (!TryReadFloat(lineK_textBox, "k", out k))
                            return null;
                        if (!TryReadFloat(lineB_textBox, "b", out b))
                            return null;
                        return new LineParameters { K = k, B = b };
                    }

                case SignalType.Noise:
                    {
                        float range;
                        if (!TryReadFloat(noiseRange_textBox, "Размах", out range))
                            return null;
                        return new NoiseParameters { Range = range };
                    }

                case SignalType.Square:
                    {
                        float period, phase, amplitude, duty;
                        if (!TryReadFloat(squarePeriod_textBox, "Период", out period))
                            return null;
                        if (!TryReadFloat(squarePhase_textBox, "Фаза", out phase))
                            return null;
                        if (!TryReadFloat(squareAmplitude_textBox, "Амплитуда", out amplitude))
                            return null;
                        if (!TryReadFloat(squareDuty_textBox, "Скважность", out duty))
                            return null;
                        return new SquareParameters { Period = period, Phase = phase, Amplitude = amplitude, DutyCycle = duty };
                    }

                case SignalType.Saw:
                    {
                        float period, phase, amplitude;
                        if (!TryReadFloat(sawPeriod_textBox, "Период", out period))
                            return null;
                        if (!TryReadFloat(sawPhase_textBox, "Фаза", out phase))
                            return null;
                        if (!TryReadFloat(sawAmplitude_textBox, "Амплитуда", out amplitude))
                            return null;
                        return new SawParameters { Period = period, Phase = phase, Amplitude = amplitude };
                    }

                case SignalType.Triangle:
                    {
                        float period, phase, amplitude;
                        if (!TryReadFloat(trianglePeriod_textBox, "Период", out period))
                            return null;
                        if (!TryReadFloat(trianglePhase_textBox, "Фаза", out phase))
                            return null;
                        if (!TryReadFloat(triangleAmplitude_textBox, "Амплитуда", out amplitude))
                            return null;
                        return new TriangleParameters { Period = period, Phase = phase, Amplitude = amplitude };
                    }

                case SignalType.Constant:
                    {
                        float value;
                        if (!TryReadFloat(constantValue_textBox, "Значение", out value))
                            return null;
                        return new ConstantParameters { Value = value };
                    }

                default:
                    return null;
            }
        }

        private void Button_Click_Generate(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            float samplingRate, duration;
            if (!TryReadFloat(freq_textBox, "Частота", out samplingRate))
                return;
            if (!TryReadFloat(len_textBox, "Длина", out duration))
                return;

            GeneratorParameters parameters = ReadParameters(GetSelectedSignalType());
            if (parameters == null)
                return;

            ISignalGenerator generator = SignalGeneratorFactory.Create(parameters);
            SingleChannelSignal signal = generator.Generate(new SamplingParameters(samplingRate, duration));
            addSignal(signal);
        });

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
                addSignal(SingleChannelSignalReader.Read(dialog.FileName));
        });

        private void Button_Click_Remove(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0)
                return;
            signalList.RemoveAt(signalList.Count - 1);
            Redraw();
        }

        private void Button_Click_Summ(object sender, RoutedEventArgs e) => RunAction(() =>
            addSignal(SignalOperations.Sum(signalList)));

        private void Button_Click_Concat(object sender, RoutedEventArgs e) => RunAction(() =>
            addSignal(SignalOperations.Concat(signalList)));

        private void Button_Click_Resample(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            SingleChannelSignal resampled = Resampler.Resample(source, ReadFloat(newFreq_textBox.Text));
            signalList[signalList.Count - 1] = resampled;
            Redraw();
        });

        private int GetWindowSize(bool requireOdd)
        {
            if (signalList.Count == 0)
                throw new InvalidOperationException("Сначала создайте или загрузите сигнал.");
            float seconds = ReadFloat(Window_textBox.Text);
            if (seconds <= 0)
                throw new ArgumentException("Длина окна должна быть положительной.");
            int size = Math.Max(1, (int)Math.Round(seconds * signalList.Last().Frequency, MidpointRounding.AwayFromZero));
            if (requireOdd && size % 2 == 0)
                size++;
            return size;
        }

        private bool UseMedianFilter => filterType_comboBox != null && filterType_comboBox.SelectedIndex == 1;

        private void Button_Click_Offline(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            int windowSize = GetWindowSize(true);
            float[] filtered = UseMedianFilter
                ? OfflineMedianFilter.Filter(source.Data, windowSize)
                : OfflineFilter.Filter(source.Data, windowSize);
            addSignal(new SingleChannelSignal(filtered, source.Frequency));
        });

        private void Button_Click_Online(object sender, RoutedEventArgs e) => RunAction(() =>
        {
            SingleChannelSignal source = GetLastSignal();
            bool median = UseMedianFilter;
            int windowSize = GetWindowSize(median);
            OnlineFilter meanFilter = median ? null : new OnlineFilter(windowSize);
            OnlineMedianFilter medianFilter = median ? new OnlineMedianFilter(windowSize) : null;
            List<float> result = new List<float>();
            const int chunkSize = 7;
            for (int offset = 0; offset < source.Data.Length; offset += chunkSize)
            {
                float[] chunk = source.Data.Skip(offset).Take(chunkSize).ToArray();
                result.AddRange(median ? medianFilter.Filter(chunk) : meanFilter.Filter(chunk));
            }
            addSignal(new SingleChannelSignal(result.ToArray(), source.Frequency));
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
            analyzer.Analyze(source.Data);
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
                WpfPlot1.Plot.AddSignal(signal.Data, signal.Frequency);
            if (signalList.Count > 0)
            {
                float freq = signalList.Last().Frequency;
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

