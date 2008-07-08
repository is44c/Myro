﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using System.Threading;

using Myro;
using Myro.GUI.WPFControls;

namespace Myro.GUI.SimpleIDE
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public static RoutedCommand SaveAll = new RoutedCommand();
        public static RoutedCommand CloseDocument = new RoutedCommand();
        public static RoutedCommand CloseAll = new RoutedCommand();
        public static RoutedCommand Run = new RoutedCommand();
        public static RoutedCommand ConfigEditor = new RoutedCommand();
        public static RoutedCommand IDEOptions = new RoutedCommand();
        //public static RoutedCommand BrowseManifest = new RoutedCommand();
        public static RoutedCommand About = new RoutedCommand();
        public static RoutedCommand Exit = new RoutedCommand();

        //string curManifest = null;
        MyroConfigFiles currentConfig = null;
        bool connected = false;
        //Robot robot = null;
        Object connectedLock = new Object();
        Thread connectionThread = null;
        Object connectionThreadLock = new Object();
        Editor editor = null;

        static Window1()
        {
            SaveAll.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            CloseDocument.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            CloseAll.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control | ModifierKeys.Shift));
            Run.InputGestures.Add(new KeyGesture(Key.F5));
        }

        public Window1()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (editor.RequestCloseAll() == false)
                    e.Cancel = true;
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
                e.Cancel = true;
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                controlPanel.Dispose();
                commandWindow.Dispose();
                Robot.Shutdown();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        //private void OnBrowseManifest(object sender, ExecutedRoutedEventArgs e)
        //{
        //    var dlg = new OpenFileDialog();
        //    if (curManifest != null && curManifest.Length > 0)
        //        dlg.FileName = curManifest;
        //    dlg.DefaultExt = ".manifest.xml";
        //    dlg.Filter = "DSS Manifest (.manifest.xml)|*.manifest.xml";
        //    if (dlg.ShowDialog(this) == true)
        //    {
        //        lock (connectionThreadLock)
        //        {
        //            curManifest = dlg.FileName;
        //            if (connectionThread == null || connectionThread.IsAlive == false)
        //            {
        //                connectionThread = new Thread(new ThreadStart(delegate() { connect(); }));
        //                connectionThread.Start();
        //            }
        //        }
        //    }
        //}

        //private void ToggleConnect(object sender, RoutedEventArgs e)
        //{
        //    if (curManifest == null || curManifest.Length <= 0)
        //        BrowseManifest(sender, e);

        //    lock (connectionThreadLock)
        //    {
        //        if (connectionThread == null || connectionThread.IsAlive == false)
        //        {
        //            connectionThread = new Thread(new ThreadStart(delegate() { connect(); }));
        //            connectionThread.Start();
        //        }
        //    }
        //}

        private void OnInitialized(object sender, EventArgs e)
        {
            try
            {
                commandWindow.StartScripting();
                commandWindow.PythonExecuting +=
                    delegate(object source, EventArgs e2)
                    {
                        //Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                        //    new ThreadStart(delegate() { runButton.BitmapEffect = new OuterGlowBitmapEffect() { GlowColor = Colors.Orange, GlowSize = 5 }; }));
                    };
                commandWindow.PythonFinished +=
                    delegate(object source, EventArgs e2)
                    {
                        //Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                        //    new ThreadStart(delegate() { runButton.BitmapEffect = null; }));
                    };
                editor = new Editor(this);
                editor.InsertedEditor += OnEditorInserted;
                editor.RemovedEditor += OnEditorRemoved;
                editor.ActivatedEditor += OnEditorActivated;
                editor.ModifiedChanged += OnEditorNameChanged;
                editor.NameChanged += OnEditorNameChanged;
                //SetButtonsEnabled();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnNew(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                editor.RequestNewDocument();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnSave(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var doc = getCurrentDocument();
                if (doc != null)
                    editor.RequestSaveDocument(doc);
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnSaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var doc = getCurrentDocument();
                if (doc != null)
                    editor.RequestSaveAs(doc);
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnOpen(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                editor.RequestOpen();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnSaveAll(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                editor.RequestSaveAll();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnCloseCurrent(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var doc = getCurrentDocument();
                if (doc != null)
                    editor.RequestClose(doc);
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnCloseAll(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                editor.RequestCloseAll();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnRun(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var editor = getCurrentEditor();
                if (editor != null)
                    commandWindow.ExecuteCommandSilently(editor.Text);
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnEditorInserted(object sender, Editor.EditorEventArgs e)
        {
            try
            {
                var header = new Grid();
                header.Children.Add(new Label() { Content = e.Document.FileName });
                header.Children.Add(new Button() { Content = new Image() { Source = new BitmapImage(new Uri("cross.png", UriKind.Relative)) } });
                var item = new CloseableTabItemDemo.CloseableTabItem()
                {
                    Header = e.Document.FileName,
                    Content = e.Document.EditorControl
                };
                item.GotFocus +=
                    delegate(object sender2, RoutedEventArgs e2)
                    {
                        e.Document.EditorControl.Focus();
                    };
                item.CloseTab +=
                    delegate(object sender2, RoutedEventArgs e2)
                    {
                        editor.RequestClose(e.Document);
                    };
                mainTabs.Items.Add(item);
                mainTabs.SelectedIndex = mainTabs.Items.Count - 1;
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnEditorRemoved(object sender, Editor.EditorEventArgs e)
        {
            try
            {
                mainTabs.Items.RemoveAt(findEditor(e.Document));
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnEditorActivated(object sender, Editor.EditorEventArgs e)
        {
            try
            {
                mainTabs.SelectedIndex = findEditor(e.Document);
                //if (mainTabs.SelectedItem != null && GetCurrentEditor() != null)
                //    FocusManager.SetFocusedElement((TabItem)mainTabs.SelectedItem, GetCurrentEditor());
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnEditorNameChanged(object sender, Editor.EditorEventArgs e)
        {
            try
            {
                if (e.Document.IsModified)
                    ((TabItem)mainTabs.Items[findEditor(e.Document)]).Header = e.Document.FileName + " *";
                else
                    ((TabItem)mainTabs.Items[findEditor(e.Document)]).Header = e.Document.FileName;
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                //SetButtonsEnabled();
                if (mainTabs.SelectedItem != null && getCurrentEditor() != null)
                    getCurrentEditor().Focus();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void HasCurrentDocument(object sender, CanExecuteRoutedEventArgs e)
        {
            try
            {
                e.CanExecute = (getCurrentEditor() != null);
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnConfigEditor(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                new ConfigEditor().Show();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnRobotChange(object sender, TopBar.RobotChangeEventArgs e)
        {
            try
            {
                if (connectionThread == null || connectionThread.IsAlive == false)
                {
                    currentConfig = e.ConfigFiles;
                    connectionThread = new Thread(new ThreadStart(delegate() { connect(); }));
                    connectionThread.Start();
                }
                else
                    e.Cancel = true;
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnAbout(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                AboutBox about = new AboutBox();
                about.ShowDialog();
            }
            catch (Exception err)
            {
                GUIUtilities.ReportUnexpectedException(err);
            }
        }

        private void OnIDEOptions(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void OnExit(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }

        //private void OnCut(object sender, RoutedEventArgs e)
        //{
        //    var editor = GetCurrentEditor();
        //    if (editor != null)
        //        editor.Cut();
        //}

        //private void OnCopy(object sender, RoutedEventArgs e)
        //{
        //    var editor = GetCurrentEditor();
        //    if (editor != null)
        //        editor.Copy();
        //}

        //private void OnPaste(object sender, RoutedEventArgs e)
        //{
        //    var editor = GetCurrentEditor();
        //    if (editor != null)
        //        editor.Paste();
        //}

        private int findEditor(Editor.EditorDocument doc)
        {
            for (int i = 0; i < mainTabs.Items.Count; i++)
                if (((TabItem)mainTabs.Items[i]).Content == doc.EditorControl)
                    return i;
            throw new ArgumentException("Editor document not found in tabs");
        }

        private Editor.EditorDocument getCurrentDocument()
        {
            if (mainTabs.SelectedIndex >= 0)
            {
                var content = getCurrentEditor();
                if (content != null)
                    foreach (var doc in editor.Documents)
                        if (content == doc.EditorControl)
                            return doc;
                return null;
            }
            else
                return null;
        }

        private TextBox getCurrentEditor()
        {
            if (mainTabs != null)
                if (mainTabs.SelectedIndex >= 0)
                    // This returns the textbox if the currently-selected tab contains
                    // one, or null if it doesn't.
                    return ((TabItem)mainTabs.Items[mainTabs.SelectedIndex]).Content as TextBox;
                else
                    return null;
            else
                return null;
        }

        //private void disconnect()
        //{
        //    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
        //        new ThreadStart(delegate()
        //        {
        //            manifestBox.Text = "Disconnecting from robot...";
        //        }));
        //    controlPanel.SetRobot(null);
        //    if (robot != null)
        //        robot.Shutdown();
        //    robot = null;
        //    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
        //        new ThreadStart(delegate()
        //        {
        //            controlPanel.IsEnabled = false;
        //            try
        //            {
        //                ((Image)connectButton.Content).Source = new BitmapImage(new Uri("disconnect.png", UriKind.Relative));
        //                manifestBox.Text = (curManifest == null ? "Disconnected" : curManifest + " (disconnected)");
        //            }
        //            catch (Exception e) { Console.WriteLine(e); }
        //        }));
        //}

        private void connect()
        {
            if (connected == false && currentConfig != null)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new ThreadStart(delegate()
                    {
                        //manifestBox.Text = "Connecting to robot...";
                        commandWindow.ExecuteCommand("from myro import *");
                        commandWindow.LogText("> init('" + currentConfig.BaseName + "')\n", Colors.MediumBlue);
                    }));
                Robot.Init(currentConfig.ManifestFilePath,
                    currentConfig.MyroConfiguration.HttpPort,
                    currentConfig.MyroConfiguration.DsspPort);
                controlPanel.SetRobot();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new ThreadStart(delegate()
                    {
                        controlPanel.IsEnabled = true;
                        try
                        {
                            //((Image)connectButton.Content).Source = new BitmapImage(new Uri("connected.png", UriKind.Relative));
                            //manifestBox.Text = (curManifest == null ? "" : curManifest);
                        }
                        catch (Exception e) { Console.WriteLine(e); }
                    }));
            }
        }

        //private void cycle()
        //{
        //    disconnect();
        //    connect();
        //}

        //private void toggle()
        //{
        //    if (robot == null)
        //        connect();
        //    else
        //        disconnect();
        //}
    }
}
