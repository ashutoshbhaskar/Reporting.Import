using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace DevExpress.XtraReports.Import {
    class Program {
        static void Main(string[] args) {
            try {
                Dictionary<string, string> argDictionary = CreateArgDictionary(args);
                string inputFile;
                if(!argDictionary.TryGetValue("/in", out inputFile))
                    throw new ArgumentException();
                string outputFile;
                if(!argDictionary.TryGetValue("/out", out outputFile))
                    throw new ArgumentException();
                string path = Path.GetFullPath(inputFile);
                if(!File.Exists(path))
                    throw new Exception("File \"" + path + "\" doesn't exist.");
                ConfigureTracer();

                ConverterBase converter = CreateConverter(Path.GetExtension(path), argDictionary, outputFile);
                ConversionResult conversionResult = converter.Convert(path);
                conversionResult.TargetReport.SaveLayoutToXml(outputFile);
            } catch(Exception ex) {
                if(ex is ArgumentException) {
                    WriteInfo();
                } else {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        static void WriteInfo() {
            string[] infos = new string[] {
                    "Imports report files of different types into an XtaReport class file.\r\n",
                    "Usage:",
                    "ReportsImport /in:path1 /out:path2\r\n",
                    "              path1 Specifies the input file's location and type.",
#if Access
                    "                    *.mdb or *.mde file matches MS Access reports.",
#endif
#if Active
                    "                    *.rpx file matches ActiveReports.",
#endif
#if Crystal
                    "                    *.rpt file matches Crystal Reports.",
                    "              /crystal:UnrecognizedFunctionBehavior=Ignore",
#endif
                    "",
                    "              path2 Specifies the output file's location.\r\n",
                    @"For more information, see https://github.com/DevExpress/Reporting.Import"
                };
            foreach(string s in infos)
                Console.WriteLine(s);
        }
        static ConverterBase CreateConverter(string extension, Dictionary<string, string> argDictionary, string outputPath) {
#if Access
            AccessReportSelectionForm.AccessIconResourceName = typeof(AccessConverter).Namespace + ".Import.AccessReport.bmp";
            if(extension == ".mdb" || extension == ".mde")
                return new AccessConverter();
#endif
#if Active
            if(extension == ".rpx")
                return new ActiveReportsConverter();
#endif
#if Crystal
            if(extension == ".rpt") {
                Dictionary<string, string> crystalProperties = CreateSubArg(argDictionary, "/crystal");
                string unrecognizedFunctionBehavior;
                if(crystalProperties.TryGetValue("UnrecognizedFunctionBehavior", out unrecognizedFunctionBehavior)) {
                    CrystalConverter.UnrecognizedFunctionBehavior = string.Equals(unrecognizedFunctionBehavior, nameof(UnrecognizedFunctionBehavior.Ignore))
                        ? UnrecognizedFunctionBehavior.Ignore
                        : UnrecognizedFunctionBehavior.InsertWarning;
                }
                var crystalConverter = new CrystalConverter();
                crystalConverter.SubreportGenerated += (_, e) => Converter_SubreportGenerated(outputPath, e);
                return crystalConverter;
            }
#endif
            throw new ArgumentException();
        }

        static Dictionary<string, string> CreateArgDictionary(string[] args) {
            if(args.Length < 2)
                throw new ArgumentException();
            Dictionary<string, string> argDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach(string arg in args) {
                string[] items = arg.Split(new char[] { ':' }, 2);
                if(items.Length < 2)
                    throw new ArgumentException();
                argDictionary.Add(items[0], items[1]);
            }
            return argDictionary;
        }
        static Dictionary<string, string> CreateSubArg(Dictionary<string, string> argDictionary, string key) {
            string subArgumentsString;
            if(!argDictionary.TryGetValue(key, out subArgumentsString))
                return new Dictionary<string, string>();
            Dictionary<string, string> subArgDictionary = subArgumentsString
                .Split(';')
                .Select(x => x.Split(new[] { '=' }, 2))
                .ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : null, StringComparer.OrdinalIgnoreCase);
            return subArgDictionary;
        }
        static void ConfigureTracer() {
            var traceSource = XtraPrinting.Tracer.GetSource("DXperience.Reporting", System.Diagnostics.SourceLevels.Error | System.Diagnostics.SourceLevels.Warning);
            var listener = new System.Diagnostics.ConsoleTraceListener();
            traceSource.Listeners.Add(listener);
        }
        static void Converter_SubreportGenerated(string outputFile, CrystalConverterSubreportGeneratedEventArgs e) {
            var subreportFile = Path.Combine(
                Path.GetDirectoryName(outputFile),
                Path.GetFileNameWithoutExtension(outputFile) + "_" + EscapeFileName(e.OriginalSubreportName) + Path.GetExtension(outputFile));
            e.SubReport.SaveLayoutToXml(subreportFile);
            e.SubreportControl.ReportSourceUrl = subreportFile;
        }
        static string EscapeFileName(string originalSubreportName) {
            foreach(char invalidChar in Path.GetInvalidFileNameChars())
                originalSubreportName = originalSubreportName.Replace(invalidChar, '_');
            return originalSubreportName;
        }
    }
}
