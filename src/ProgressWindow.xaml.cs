﻿using System;
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
using System.Windows.Shapes;

namespace PTVersionDownloader
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private int Max = 0;
        private int Count = 0;
        public string TitleText = "";

        public ProgressWindow(string titleText)
        {
            InitializeComponent();

            TitleText = titleText;
            Progress.Value = 0;
            UpdateTitleText();
        }

        public void SetMax(int max)
        {
            Max = max;
            Progress.Maximum = Max;
            UpdateTitleText();
        }

        private void UpdateTitleText()
        {
            Text.Text = $"{TitleText} ({Count}/{Max})";
        }
        public void Increment()
        {
            Count++;
            Progress.Value = Count;
            UpdateTitleText();
        }
    }
}
