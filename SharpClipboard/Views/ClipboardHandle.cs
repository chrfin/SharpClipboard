﻿/*
 * The SharpClipboard Handle.
 * ----------------------------------------
 * Please preserve this window.
 * It acts as the message-processing
 * handle with regards to the clipboard. 
 *
 * The window however will not be visible
 * to the users both via the Taskbar or 
 * the Task Manager so don't you worry :)
 * ----------------------------------------
 * 
 */


using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WK.Libraries.SharpClipboardNS.Views
{
    public partial class ClipboardHandle : Form
    {
        #region Constructor

        public ClipboardHandle()
        {
            InitializeComponent();
        }

        #endregion

        #region Fields

        private IntPtr _chainedWnd;
        
        const int WM_DRAWCLIPBOARD = 0x0308;
        const int WM_CHANGECBCHAIN = 0x030D;

        private string _processName = string.Empty;
        private string _executableName = string.Empty;
        private string _executablePath = string.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets an active <see cref="SharpClipboard"/> instance
        /// for use when managing the current clipboard handle.
        /// </summary>
        [Browsable(false)]
        public SharpClipboard SharpClipboardInstance { get; set; }
        
        #endregion

        #region Methods

        #region Clipboard Management

        #region Win32 Integration

        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll")]
        static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Clipboard Monitor

        /// <summary>
        /// Modifications in this overriden method have
        /// been added to disable viewing of the handle-
        /// window in the Task Manager.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get {
                
                
                var cp = base.CreateParams;

                // Turn on WS_EX_TOOLWINDOW.
                cp.ExStyle |= 0x80;

                return cp;

            }
        }
        
        /// <summary>
        /// This is the main clipboard detection method.
        /// Algorithmic customizations are most welcome.
        /// </summary>
        /// <param name="m">The processed window-reference message.</param>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:

                    // If clipboard-monitoring is enabled, proceed to listening.
                    if (SharpClipboardInstance.MonitorClipboard)
                    {
                        IDataObject dataObj = Clipboard.GetDataObject();

                        // Determines whether a file/files have been cut/copied.
                        if ((SharpClipboardInstance.ObservableFormats.Files == true) &&
                            (dataObj.GetDataPresent(DataFormats.FileDrop)))
                        {
                            string[] capturedFiles = (string[])dataObj.GetData(DataFormats.FileDrop);

                            SharpClipboardInstance.ClipboardFiles.AddRange(capturedFiles);
                            SharpClipboardInstance.ClipboardFile = capturedFiles[0];

                            SharpClipboardInstance.Invoke(capturedFiles, SharpClipboard.ContentTypes.Files, 
                                new SourceApplication(GetForegroundWindow(), SharpClipboardInstance.ForegroundWindowHandle(),
                                GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }

                        // Determines whether text has been cut/copied.
                        else if ((SharpClipboardInstance.ObservableFormats.Texts == true) && 
                                 (dataObj.GetDataPresent(DataFormats.Text)))
                        {
                            string capturedText = dataObj.GetData(DataFormats.Text) as string;
                            SharpClipboardInstance.ClipboardText = capturedText;

                            SharpClipboardInstance.Invoke(capturedText, SharpClipboard.ContentTypes.Text,
                                new SourceApplication(GetForegroundWindow(), SharpClipboardInstance.ForegroundWindowHandle(),
                                GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }

                        // Determines whether an image has been cut/copied.
                        else if ((SharpClipboardInstance.ObservableFormats.Images == true) && 
                                 (dataObj.GetDataPresent(DataFormats.Bitmap)))
                        {
                            Image capturedImage = dataObj.GetData(DataFormats.Bitmap) as Image;
                            SharpClipboardInstance.ClipboardImage = capturedImage;

                            SharpClipboardInstance.Invoke(capturedImage, SharpClipboard.ContentTypes.Image,
                                new SourceApplication(GetForegroundWindow(), SharpClipboardInstance.ForegroundWindowHandle(),
                                GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }
                    }

                    break;

                case WM_CHANGECBCHAIN:

                    if (m.WParam == _chainedWnd)
                        _chainedWnd = m.LParam;
                    else
                        SendMessage(_chainedWnd, m.Msg, m.WParam, m.LParam);

                    break;
            }
        }

        #endregion

        #region Helper Methods

        public void StartMonitoring()
        {
            this.Show();
        }

        public void StopMonitoring()
        {
            this.Close();
        }

        #endregion

        #endregion

        #region Source App Management
        
        #region Win32 Externals

        [DllImport("user32.dll")]
        static extern int GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindowPtr();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32")]
        private static extern UInt32 GetWindowThreadProcessId(Int32 hWnd, out Int32 lpdwProcessId);

        #endregion

        #region Helper Methods

        private Int32 GetProcessID(Int32 hwnd)
        {
            Int32 processID = 1;
            GetWindowThreadProcessId(hwnd, out processID);

            return processID;
        }

        private string GetApplicationName()
        {
            try
            {
                Int32 hwnd = 0;
                hwnd = GetForegroundWindow();

                _processName = Process.GetProcessById(GetProcessID(hwnd)).ProcessName;
                _executablePath = Process.GetProcessById(GetProcessID(hwnd)).MainModule.FileName;
                _executableName = _executablePath.Substring(_executablePath.LastIndexOf(@"\") + 1);
            }
            catch (Exception) { }

            return _executableName;
        }

        private string GetApplicationPath()
        {
            return _executablePath;
        }

        private string GetActiveWindowTitle()
        {
            const int capacity = 256;
            StringBuilder content = null;
            IntPtr handle = IntPtr.Zero;

            try
            {
                content = new StringBuilder(capacity);
                handle = SharpClipboardInstance.ForegroundWindowHandle();
            }
            catch (Exception) { }

            if (GetWindowText(handle, content, capacity) > 0)
                return content.ToString();

            return null;
        }

        #endregion

        #endregion

        #endregion

        #region Events

        private void OnLoad(object sender, EventArgs e)
        {
            // Start listening for clipboard changes.
            _chainedWnd = SetClipboardViewer(this.Handle);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop listening to clipboard changes.
            ChangeClipboardChain(this.Handle, _chainedWnd);

            _chainedWnd = IntPtr.Zero;
        }

        #endregion
    }
}