using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PRISM;
using PRISM.Logging;

namespace PeptideListToXML
{
    // This program reads a tab-delimited text file of peptide sequence and
    // creates a PepXML or mzIdentML file with the appropriate information
    //
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Program started April 13, 2012
    //
    // E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    // Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    // -------------------------------------------------------------------------------
    //
    // Licensed under the Apache License, Version 2.0; you may not use this file except
    // in compliance with the License. You may obtain a copy of the License at
    // http://www.apache.org/licenses/LICENSE-2.0

    /// <summary>
    /// Entry class for the .exe
    /// </summary>
    internal static class Program
    {
        // Ignore Spelling: mzIdentML

        private const string PROGRAM_DATE = "June 17, 2021";

        private static string mInputFilePath;
        private static string mOutputDirectoryPath;             // Optional
        private static string mParameterFilePath;            // Optional
        private static string mFastaFilePath;
        private static string mSearchEngineParamFileName;
        private static int mHitsPerSpectrum;              // Number of hits per spectrum to store; 0 means to store all hits
        private static bool mPreview;
        private static bool mSkipXPeptides;
        private static bool mTopHitOnly;
        private static int mMaxProteinsPerPSM;
        private static string mPeptideFilterFilePath;
        private static List<int> mChargeFilterList;
        private static bool mLoadModsAndSeqInfo;
        private static bool mLoadMSGFResults;
        private static bool mLoadScanStats;

        // Future enum; mzIdentML is not yet supported
        // Private mOutputFormat As clsPeptideListToXML.PeptideListOutputFormat

        private static string mOutputDirectoryAlternatePath;                // Optional
        private static bool mRecreateDirectoryHierarchyInAlternatePath;     // Optional
        private static bool mRecurseDirectories;
        private static int mRecurseDirectoriesMaxLevels;
        private static bool mLogMessagesToFile;

        // Unused: private string mLogFilePath;
        // Unused: private string mLogDirectoryPath;

        private static PeptideListToXML mPeptideListConverter;
        private static DateTime mLastProgressReportTime;
        private static DateTime mLastPercentDisplayed;

        /// <summary>
        /// Program entry point
        /// </summary>
        /// <returns>0 if no error, error code if an error</returns>
        public static int Main()
        {
            var commandLineParser = new clsParseCommandLine();

            // Initialize the options
            mInputFilePath = string.Empty;
            mOutputDirectoryPath = string.Empty;
            mParameterFilePath = string.Empty;
            mFastaFilePath = string.Empty;
            mSearchEngineParamFileName = string.Empty;
            mHitsPerSpectrum = 3;
            mPreview = false;
            mSkipXPeptides = false;
            mTopHitOnly = false;
            mMaxProteinsPerPSM = 100;
            mPeptideFilterFilePath = string.Empty;
            mChargeFilterList = new List<int>();
            mLoadModsAndSeqInfo = true;
            mLoadMSGFResults = true;
            mLoadScanStats = true;

            // Future enum; mzIdentML is not yet supported
            // mOutputFormat = clsPeptideListToXML.PeptideListOutputFormat.PepXML

            mRecurseDirectories = false;
            mRecurseDirectoriesMaxLevels = 0;
            mLogMessagesToFile = false;

            // Unused: mLogFilePath = String.Empty
            // Unused: mLogDirectoryPath = String.Empty

            try
            {
                var proceed = false;
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        proceed = true;
                }

                if (!proceed || commandLineParser.NeedToShowHelp || commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 || mInputFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }

                // Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
                mPeptideListConverter = new PeptideListToXML()
                {
                    LogMessagesToFile = mLogMessagesToFile,
                    FastaFilePath = mFastaFilePath,
                    SearchEngineParamFileName = mSearchEngineParamFileName,
                    HitsPerSpectrum = mHitsPerSpectrum,
                    PreviewMode = mPreview,
                    SkipXPeptides = mSkipXPeptides,
                    TopHitOnly = mTopHitOnly,
                    MaxProteinsPerPSM = mMaxProteinsPerPSM,
                    PeptideFilterFilePath = mPeptideFilterFilePath,
                    LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                    LoadMSGFResults = mLoadMSGFResults,
                    LoadScanStats = mLoadScanStats
                };

                mPeptideListConverter.ChargeFilterList.AddRange(mChargeFilterList);

                RegisterEvents(mPeptideListConverter);

                mLastProgressReportTime = DateTime.UtcNow;
                mLastPercentDisplayed = DateTime.UtcNow;
                if (mRecurseDirectories)
                {
                    if (mPeptideListConverter.ProcessFilesAndRecurseDirectories(mInputFilePath, mOutputDirectoryPath, mOutputDirectoryAlternatePath, mRecreateDirectoryHierarchyInAlternatePath, mParameterFilePath, mRecurseDirectoriesMaxLevels))
                    {
                        return 0;
                    }

                    return (int)mPeptideListConverter.ErrorCode;
                }

                if (mPeptideListConverter.ProcessFilesWildcard(mInputFilePath, mOutputDirectoryPath, mParameterFilePath))
                {
                    return 0;
                }

                var returnCode = (int)mPeptideListConverter.ErrorCode;
                if (returnCode != 0)
                {
                    ShowErrorMessage("Error while processing: " + mPeptideListConverter.GetErrorMessage());
                }

                return returnCode;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main", ex);
                return -1;
            }
        }

        private static void DisplayProgressPercent(string taskDescription, int percentComplete, bool addCarriageReturn)
        {
            if (addCarriageReturn)
            {
                Console.WriteLine();
            }

            if (percentComplete > 100)
                percentComplete = 100;

            if (string.IsNullOrEmpty(taskDescription))
                taskDescription = "Processing";

            Console.Write(taskDescription + ": " + percentComplete.ToString() + "% ");
            if (addCarriageReturn)
            {
                Console.WriteLine();
            }
        }

        private static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            var validParameters = new List<string>
            {
                "I", "O", "F", "E", "H", "X",
                "PepFilter", "ChargeFilter", "TopHitOnly", "MaxProteins",
                "NoMods", "NoMSGF", "NoScanStats",
                "Preview", "P", "S", "A", "R", "L"
            };

            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    ShowErrorMessage("Invalid command line parameters", (from item in commandLineParser.InvalidParameters(validParameters)
                                                                         select ("/" + item)).ToList());
                    return false;
                }

                // Query commandLineParser to see if various parameters are present
                if (commandLineParser.RetrieveValueForParameter("I", out var inputFilePath))
                {
                    mInputFilePath = inputFilePath;
                }
                else if (commandLineParser.NonSwitchParameterCount > 0)
                {
                    mInputFilePath = commandLineParser.RetrieveNonSwitchParameter(0);
                }

                if (commandLineParser.RetrieveValueForParameter("O", out var outputDirectoryPath))
                    mOutputDirectoryPath = outputDirectoryPath;

                // Future enum; mzIdentML is not yet supported
                // if (.RetrieveValueForParameter("M", value)) {
                //     mOutputFormat = clsPeptideListToXML.PeptideListOutputFormat.mzIdentML
                // }

                if (commandLineParser.RetrieveValueForParameter("F", out var fastaFilePath))
                    mFastaFilePath = fastaFilePath;

                if (commandLineParser.RetrieveValueForParameter("E", out var searchEngineParamFileName))
                    mSearchEngineParamFileName = searchEngineParamFileName;

                if (commandLineParser.RetrieveValueForParameter("H", out var hitsPerSpectrum) &&
                    int.TryParse(hitsPerSpectrum, out var hitsPerSpectrumValue))
                {
                    mHitsPerSpectrum = hitsPerSpectrumValue;
                }

                if (commandLineParser.IsParameterPresent("X"))
                    mSkipXPeptides = true;

                if (commandLineParser.IsParameterPresent("TopHitOnly"))
                    mTopHitOnly = true;

                if (commandLineParser.RetrieveValueForParameter("MaxProteins", out var maxProteins) &&
                    int.TryParse(maxProteins, out var maxProteinsValue))
                {
                    mMaxProteinsPerPSM = maxProteinsValue;
                }

                if (commandLineParser.RetrieveValueForParameter("PepFilter", out var peptideFilterFilePath))
                    mPeptideFilterFilePath = peptideFilterFilePath;

                if (commandLineParser.RetrieveValueForParameter("ChargeFilter", out var chargeFilter))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(chargeFilter))
                        {
                            ShowErrorMessage("ChargeFilter argument must have one or more charges, for example /ChargeFilter:2  or /ChargeFilter:2,3");
                            Console.WriteLine();
                            return false;
                        }

                        foreach (var charge in chargeFilter.Split(',').ToList())
                        {
                            if (int.TryParse(charge, out var chargeValue))
                            {
                                mChargeFilterList.Add(chargeValue);
                            }
                            else
                            {
                                ShowErrorMessage("Invalid charge specified: " + charge);
                                Console.WriteLine();
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowErrorMessage("Error parsing the list of charges \"" + chargeFilter + "\"; should be a command separated list", ex);
                        Console.WriteLine();
                        return false;
                    }
                }

                if (commandLineParser.RetrieveValueForParameter("P", out var parameterFilePath))
                    mParameterFilePath = parameterFilePath;

                if (commandLineParser.IsParameterPresent("NoMods"))
                    mLoadModsAndSeqInfo = false;

                if (commandLineParser.IsParameterPresent("NoMSGF"))
                    mLoadMSGFResults = false;

                if (commandLineParser.IsParameterPresent("NoScanStats"))
                    mLoadScanStats = false;

                if (commandLineParser.IsParameterPresent("Preview"))
                    mPreview = true;

                if (commandLineParser.RetrieveValueForParameter("S", out var recurseDirectories))
                {
                    mRecurseDirectories = true;
                    if (!int.TryParse(recurseDirectories, out mRecurseDirectoriesMaxLevels))
                    {
                        mRecurseDirectoriesMaxLevels = 0;
                    }
                }

                if (commandLineParser.RetrieveValueForParameter("A", out var outputDirectoryAlternatePath))
                    mOutputDirectoryAlternatePath = outputDirectoryAlternatePath;

                if (commandLineParser.IsParameterPresent("R"))
                    mRecreateDirectoryHierarchyInAlternatePath = true;

                if (commandLineParser.IsParameterPresent("L"))
                    mLogMessagesToFile = true;

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters", ex);
            }

            return false;
        }

        private static void ShowErrorMessage(string errorMessage, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(errorMessage, ex);
        }

        private static void ShowErrorMessage(string title, IEnumerable<string> errorMessages)
        {
            ConsoleMsgUtils.ShowErrors(title, errorMessages);
        }

        private static void ShowProgramHelp()
        {
            try
            {
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "This program reads a tab-delimited text file created by the Peptide Hit Results Processor (PHRP) " +
                    "and creates a PepXML with the appropriate information. The various _SeqInfo files created by PHRP " +
                    "must be present in the same directory as the text file. " +
                    "If the MASIC Scan Stats file is also present, elution time information will be extracted and included in the PepXML file. " +
                    "You should ideally also include the name of the parameter file used for the MS/MS search engine."));
                Console.WriteLine();
                Console.WriteLine("Program syntax:");
                Console.WriteLine(Path.GetFileName(Assembly.GetExecutingAssembly().Location) + " /I:PHRPResultsFile [/O:OutputDirectoryPath]");
                Console.WriteLine(" [/E:SearchEngineParamFileName] [/F:FastaFilePath] [/P:ParameterFilePath]");
                Console.WriteLine(" [/H:HitsPerSpectrum] [/X] [/TopHitOnly] [/MaxProteins:" + PeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM + "]");
                Console.WriteLine(" [/PepFilter:PeptideFilterFilePath] [/ChargeFilter:ChargeList]");
                Console.WriteLine(" [/NoMods] [/NoMSGF] [/NoScanStats] [/Preview]");
                Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputDirectoryPath] [/R] [/L] [/Q]");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "The input file path can contain the wildcard character * and should point to a tab-delimited text file created by PHRP " +
                    "(for example, Dataset_syn.txt, Dataset_xt.txt, Dataset_msgfplus_syn.txt or Dataset_inspect_syn.txt) " +
                    "The output directory switch is optional. If omitted, the output file will be created in the same directory as the input file"));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /E to specify the name of the parameter file used by the MS/MS search engine " +
                    "(must be in the same directory as the PHRP results file). For X!Tandem results, " +
                    "the default_input.xml and taxonomy.xml files must also be present in the input directory."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /F to specify the path to the fasta file to store in the PepXML file; " +
                    "ignored if /E is provided and the search engine parameter file defines the fasta file to search " +
                    "(this is the case for SEQUEST and X!Tandem but not Inspect or MS-GF+)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /H to specify the number of matches per spectrum to store " +
                    "(default is " + PeptideListToXML.DEFAULT_HITS_PER_SPECTRUM + "; use 0 to keep all hits)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /X to specify that peptides with X residues should be skipped"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /TopHitOnly to specify that each scan should only include a single peptide match (regardless of charge)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /MaxProteins to define the maximum number of proteins to track for each PSM (default is " + PeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM + ")"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /PepFilter:File to use a text file to filter the peptides included in the output file (one peptide sequence per line)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /ChargeFilter:ChargeList to specify one or more charges to filter on for inclusion in the output file. Examples:"));
                Console.WriteLine("  Only 2+ peptides:    /ChargeFilter:2");
                Console.WriteLine("  2+ and 3+ peptides:  /ChargeFilter:2,3");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph
                    ("By default, the _ModSummary file and SeqInfo files are loaded and used to determine the modified residues; " +
                     "use /NoMods to skip these files"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "By default, the _MSGF.txt file is loaded to associated MSGF SpecProb values with the results; " +
                    "use /NoMSGF to skip this file"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "By default, the MASIC _ScanStats.txt and _ScanStatsEx.txt files are loaded " +
                    "to determine elution times for scan numbers; use /NoScanStats to skip these files"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /Preview to preview the files that would be required for the specified dataset " +
                    "(taking into account the other command line switches used)"));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /P to specify a parameter file to use. " +
                    "Options in this file will override options specified for /E, /F, /H, and /X"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /S to process all valid files in the input directory and subdirectories. " +
                    "Include a number after /S (like /S:2) to limit the level of subdirectories to examine. " +
                    "When using /S, you can redirect the output of the results using /A. " +
                    "When using /S, you can use /R to re-create the input directory hierarchy in the alternate output directory (if defined)."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /L to log messages to a file. If /Q is used, no messages will be displayed at the console."));
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();
                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                Thread.Sleep(750);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax", ex);
            }
        }

        private static void RegisterEvents(IEventNotifier processingClass)
        {
            processingClass.DebugEvent += ProcessingClass_DebugEvent;
            processingClass.ErrorEvent += ProcessingClass_ErrorEvent;
            processingClass.StatusEvent += ProcessingClass_StatusEvent;
            processingClass.WarningEvent += ProcessingClass_WarningEvent;
            processingClass.ProgressUpdate += ProcessingClass_ProgressUpdate;
        }

        private static void ProcessingClass_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void ProcessingClass_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ProcessingClass_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void ProcessingClass_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static void ProcessingClass_ProgressUpdate(string progressMessage, float percentComplete)
        {
            const int PROGRESS_DOT_INTERVAL_MSEC = 250;
            if (DateTime.UtcNow.Subtract(mLastPercentDisplayed).TotalSeconds >= 15d)
            {
                Console.WriteLine();
                DisplayProgressPercent(progressMessage, (int)Math.Round(percentComplete), false);
                mLastPercentDisplayed = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC)
            {
                mLastProgressReportTime = DateTime.UtcNow;
                Console.Write(".");
            }
        }
    }
}
