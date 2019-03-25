using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

// ReSharper disable InvertIf

namespace GrabDetectSort
{
    internal class Program
    {
        public static string DarknetPath = @"C:\projects\FutureNNAimbot\FutureNNAimbot\bin\Release\darknet\";
        public static string MoviePath = @"C:\Fraps\Movies\";
        public static string FfmpegExe = @"C:\portable\ffmpeg-4.1.1-win64-static\bin\ffmpeg.exe";
        public const int BlockSize = 416;
        public static string GameName = @"r5apex";

        #region Dont touch this
        public static string DetectionThreshold = "0.15";
        public static string SourceImagePath = DarknetPath + @"data\img\";
        public static string TmpDirectory = DarknetPath + @"data\img2\";
        public static string TrainTxtFile = DarknetPath + @"data\train.txt";
        public static string MainTxtFile = DarknetPath + $@"data\{GameName}.txt";
        public static string ResultFile = DarknetPath + @"result.txt";
        public static string OutPath = DarknetPath + @"data\sorted\";
        public static List<string> ConfidenceListLow = new List<string>();
        public static List<string> ConfidenceListMid = new List<string>();
        public static List<string> ConfidenceListHigh = new List<string>();
        public static string FullFilenamePattern = @"[\w\-]*.png";
        public static string FilenamePattern = @"[\w\-]*";
        public static string ConfidencePattern = @"[0-9]*%";
        public static string NumberPattern = @"[0-9]*";
        #endregion

        private static void Main(/*string[] args*/)
        {
            try
            {
                VerifyUserPaths();
                ClearTmpDir();
                FixFileNames();
                GetFrames(TmpDirectory);
                CreateImageList(TmpDirectory, "img2");
                LabelImages();
                SortImages();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadLine();
                throw;
            }
        }

        private static void SortImages()
        {
            string line;
            var resultFile = new StreamReader(ResultFile);
            while ((line = resultFile.ReadLine()) != null)
            {
                #region Extracting filename
                if (!line.Contains("Enter Image Path")) continue;
                var fullFileName = Regex.Match(line, FullFilenamePattern);
                if (!fullFileName.Success) continue;
                var fileName = Regex.Match(fullFileName.Value, FilenamePattern);
                #endregion
                #region Extracting confidense
                var nextChar = (char)resultFile.Peek();
                if (nextChar == 0 || nextChar == 'E') continue;
                line = resultFile.ReadLine();
                if (line == null) continue;
                var confidenceStr1 = Regex.Match(line, ConfidencePattern);
                var confidenceStr2 = Regex.Match(confidenceStr1.Value, NumberPattern);
                var confidence = int.Parse(confidenceStr2.Value);
                #endregion
                #region Adding to different lists
                if (confidence >= 15 && confidence < 40)
                    ConfidenceListLow.Add(fileName.Value);
                else if (confidence >= 40 && confidence < 60)
                    ConfidenceListMid.Add(fileName.Value);
                else if (confidence >= 60 && confidence <= 100)
                    ConfidenceListHigh.Add(fileName.Value);
                #endregion
                #region Create directories
                if (!Directory.Exists(OutPath)) Directory.CreateDirectory(OutPath);
                //if (!Directory.Exists(OutPath + @"zero\")) Directory.CreateDirectory(OutPath + @"zero\");
                if (!Directory.Exists(OutPath + @"low\")) Directory.CreateDirectory(OutPath + @"low\");
                if (!Directory.Exists(OutPath + @"mid\")) Directory.CreateDirectory(OutPath + @"mid\");
                if (!Directory.Exists(OutPath + @"high\")) Directory.CreateDirectory(OutPath + @"high\");
                #endregion
                #region Scan all files and sort them
                var files = Directory.GetFiles(TmpDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var ext = Path.GetExtension(file);
                    string newFile = null;
                    if (ConfidenceListHigh.Contains(name))
                    {
                        newFile = OutPath + @"high\" + name + ext;
                    }
                    else if (ConfidenceListMid.Contains(name))
                    {
                        newFile = OutPath + @"mid\" + name + ext;
                    }
                    else if (ConfidenceListLow.Contains(name))
                    {
                        newFile = OutPath + @"low\" + name + ext;

                    }
                    if (newFile != null)
                    {
                        File.Copy(file, newFile, true);
                    }
                }
                #endregion
            }
        }

        private static void LabelImages()
        {
            //darknet.exe detector test data/r5apex.data r5apex.cfg data\backup\r5apex_last.weights -dont_show -ext_output -save_labels < data/new_train.txt > result.txt
            var arguments = $@"darknet detector test data\\{GameName}.data r5apex.cfg data\\backup\\r5apex_last.weights -thresh {DetectionThreshold} -dont_show -ext_output -save_labels < data\\train.txt > result.txt";
            Console.WriteLine(arguments);
            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                WorkingDirectory = DarknetPath,
                RedirectStandardInput = true
            };
            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();
                Thread.Sleep(1000);
                process.StandardInput.WriteLine(arguments);
                Thread.Sleep(1000);
                while (Process.GetProcessesByName("darknet").Length > 0)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private static void CreateImageList(string directory, string alias)
        {
            var files = new DirectoryInfo(directory).GetFiles($"{GameName}*.png");
            var pathOfImg = files.Aggregate("", (current, file) => current + $"data/{alias}/{file.Name}\r\n");
            File.WriteAllText(TrainTxtFile, pathOfImg);
            File.WriteAllText(MainTxtFile, pathOfImg);
        }

        private static void GetFrames(string directory)
        {
            var files = Directory.GetFiles(MoviePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                //...A1..A2...
                //.....A0.....
                //...A3..A4...
                RunFfmpeg(file, "3", BlockSize, 752, 332, "A0", directory);
                //RunFfmpeg(file, "2", BlockSize, 544, 124, "A1", TmpDirectory);
                //RunFfmpeg(file, "2", BlockSize, 960, 124, "A2", TmpDirectory);
                //RunFfmpeg(file, "2", BlockSize, 544, 540, "A3", TmpDirectory);
                //RunFfmpeg(file, "2", BlockSize, 960, 540, "A4", TmpDirectory);
            }
        }

        private static void RunFfmpeg(string fullFilename, string fps, int blockSize, int startX, int startY, string filePrefix, string tmpDir)
        {
            var filename = Path.GetFileNameWithoutExtension(fullFilename);
            var arguments =
                $@"-i {fullFilename} -skip_frame nokey -vf fps={fps},crop={blockSize}:{blockSize}:{startX}:{startY} {tmpDir}{GameName}{filePrefix}_{filename}_%06d.png";
            var processStartInfo = new ProcessStartInfo(FfmpegExe, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();
                process.WaitForExit();
            }
        }

        private static void FixFileNames()
        {
            var files = Directory.GetFiles(MoviePath, "*.avi", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.Contains('-') || file.Contains(' '))
                {
                    File.Move(file, file.Replace('-','_').Replace(' ', '_'));
                }
            }
        }

        private static void VerifyUserPaths()
        {
            if (!Directory.Exists(SourceImagePath))
            {
                Console.WriteLine("Main image directory " + SourceImagePath + " is empty");
                Console.ReadKey();
                Environment.Exit(1);
            }
            if (!Directory.Exists(MoviePath))
            {
                Console.WriteLine("Video directory " + MoviePath + " is empty");
                Console.ReadKey();
                Environment.Exit(2);
            }
            if (!File.Exists(FfmpegExe))
            {
                Console.WriteLine("Path to ffmpeg " + FfmpegExe + " is empty");
                Console.ReadKey();
                Environment.Exit(3);
            }
            if (Directory.Exists(OutPath))
            {
                Console.WriteLine("Output path must be empty " + OutPath);
                Console.ReadKey();
                Environment.Exit(4);
            }
        }

        private static void ClearTmpDir()
        {
            if (Directory.Exists(TmpDirectory))
            {
                Directory.Delete(TmpDirectory, true);
                Thread.Sleep(1000);
            }
            Directory.CreateDirectory(TmpDirectory);
            Thread.Sleep(1000);
        }
    }
}
