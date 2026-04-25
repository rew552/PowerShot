﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PowerShot
{
    public class SessionState
    {
        public string LastDirectory { get; set; }
        public string LastPrefix { get; set; }
        public int LastSequenceDigits { get; set; }
        public bool IsSystemInfoVisible { get; set; }

        public string LastFormat { get; set; }

        public SessionState()
        {
            LastDirectory = "";
            LastPrefix = "";
            LastSequenceDigits = 3;
            IsSystemInfoVisible = false;
            LastFormat = "jpg";
        }
    }
}
