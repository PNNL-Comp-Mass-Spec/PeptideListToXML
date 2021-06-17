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

    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Program started April 13, 2012

    // E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    // Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    // -------------------------------------------------------------------------------
    // 
    // Licensed under the Apache License, Version 2.0; you may not use this file except
    // in compliance with the License.  You may obtain a copy of the License at
    // http://www.apache.org/licenses/LICENSE-2.0

    static class modMain
    {

        // Ignore Spelling: mzIdentML

        public const string PROGRAM_DATE = "June 17, 2021";
        private static string mInputFilePath;
        private static string mOutputFolderPath;             // Optional
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

        private static string mOutputFolderAlternatePath;                // Optional
        private static bool mRecreateFolderHierarchyInAlternatePath;  // Optional
        private static bool mRecurseFolders;
        private static int mRecurseFoldersMaxLevels;
        private static bool mLogMessagesToFile;
        // Unused: Private mLogFilePath As String = String.Empty
        // Unused: Private mLogFolderPath As String = String.Empty

        private static clsPeptideListToXML mPeptideListConverter;
        private static DateTime mLastProgressReportTime;
        private static DateTime mLastPercentDisplayed;

        /// <summary>
    /// Program entry point
    /// </summary>
    /// <returns>0 if no error, error code if an error</returns>
    /// <remarks></remarks>
        public static int Main()
        {
            int intReturnCode;
            var commandLineParser = new clsParseCommandLine();
            bool blnProceed;

            // Initialize the options
            mInputFilePath = string.Empty;
            mOutputFolderPath = string.Empty;
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

            mRecurseFolders = false;
            mRecurseFoldersMaxLevels = 0;
            mLogMessagesToFile = false;
            // Unused: mLogFilePath = String.Empty
            // Unused: mLogFolderPath = String.Empty

            try
            {
                blnProceed = false;
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        blnProceed = true;
                }

                if (!blnProceed || commandLineParser.NeedToShowHelp || commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 || mInputFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    intReturnCode = -1;
                }
                else
                {
                    // Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined

                    mPeptideListConverter = new clsPeptideListToXML()
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
                        ChargeFilterList = mChargeFilterList,
                        LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                        LoadMSGFResults = mLoadMSGFResults,
                        LoadScanStats = mLoadScanStats
                    };
                    RegisterEvents(mPeptideListConverter);
                    mLastProgressReportTime = DateTime.UtcNow;
                    mLastPercentDisplayed = DateTime.UtcNow;
                    if (mRecurseFolders)
                    {
                        if (mPeptideListConverter.ProcessFilesAndRecurseDirectories(mInputFilePath, mOutputFolderPath, mOutputFolderAlternatePath, mRecreateFolderHierarchyInAlternatePath, mParameterFilePath, mRecurseFoldersMaxLevels))
                        {
                            intReturnCode = 0;
                        }
                        else
                        {
                            intReturnCode = (int)mPeptideListConverter.ErrorCode;
                        }
                    }
                    else if (mPeptideListConverter.ProcessFilesWildcard(mInputFilePath, mOutputFolderPath, mParameterFilePath))
                    {
                        intReturnCode = 0;
                    }
                    else
                    {
                        intReturnCode = (int)mPeptideListConverter.ErrorCode;
                        if (intReturnCode != 0)
                        {
                            ShowErrorMessage("Error while processing: " + mPeptideListConverter.GetErrorMessage());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main", ex);
                intReturnCode = -1;
            }

            return intReturnCode;
        }

        private static void DisplayProgressPercent(string taskDescription, int intPercentComplete, bool blnAddCarriageReturn)
        {
            if (blnAddCarriageReturn)
            {
                Console.WriteLine();
            }

            if (intPercentComplete > 100)
                intPercentComplete = 100;
            if (string.IsNullOrEmpty(taskDescription))
                taskDescription = "Processing";
            Console.Write(taskDescription + ": " + intPercentComplete.ToString() + "% ");
            if (blnAddCarriageReturn)
            {
                Console.WriteLine();
            }
        }

        private static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            string strValue = string.Empty;
            var lstValidParameters = new List<string>() { "I", "O", "F", "E", "H", "X", "PepFilter", "ChargeFilter", "TopHitOnly", "MaxProteins", "NoMods", "NoMSGF", "NoScanStats", "Preview", "P", "S", "A", "R", "L" };
            int intValue;
            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(lstValidParameters))
                {
                    ShowErrorMessage("Invalid command line parameters", (from item in commandLineParser.InvalidParameters(lstValidParameters)
                                                                         select ("/" + item)).ToList());
                    return false;
                }
                else
                {
                    // Query commandLineParser to see if various parameters are present
                    if (commandLineParser.RetrieveValueForParameter("I", out strValue))
                    {
                        mInputFilePath = strValue;
                    }
                    else if (commandLineParser.NonSwitchParameterCount > 0)
                    {
                        mInputFilePath = commandLineParser.RetrieveNonSwitchParameter(0);
                    }

                    if (commandLineParser.RetrieveValueForParameter("O", out strValue))
                        mOutputFolderPath = strValue;

                    // Future enum; mzIdentML is not yet supported
                    // If .RetrieveValueForParameter("M", strValue) Then
                    // mOutputFormat = clsPeptideListToXML.PeptideListOutputFormat.mzIdentML
                    // End If

                    if (commandLineParser.RetrieveValueForParameter("F", out strValue))
                        mFastaFilePath = strValue;
                    if (commandLineParser.RetrieveValueForParameter("E", out strValue))
                        mSearchEngineParamFileName = strValue;
                    if (commandLineParser.RetrieveValueForParameter("H", out strValue))
                    {
                        if (int.TryParse(strValue, out intValue))
                        {
                            mHitsPerSpectrum = intValue;
                        }
                    }

                    if (commandLineParser.IsParameterPresent("X"))
                        mSkipXPeptides = true;
                    if (commandLineParser.IsParameterPresent("TopHitOnly"))
                        mTopHitOnly = true;
                    if (commandLineParser.RetrieveValueForParameter("MaxProteins", out strValue))
                    {
                        if (int.TryParse(strValue, out intValue))
                        {
                            mMaxProteinsPerPSM = intValue;
                        }
                    }

                    if (commandLineParser.RetrieveValueForParameter("PepFilter", out strValue))
                        mPeptideFilterFilePath = strValue;
                    if (commandLineParser.RetrieveValueForParameter("ChargeFilter", out strValue))
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(strValue))
                            {
                                ShowErrorMessage("ChargeFilter switch must have one or more charges, for example /ChargeFilter:2  or /ChargeFilter:2,3");
                                Console.WriteLine();
                                return false;
                            }
                            else
                            {
                                foreach (string strCharge in strValue.Split(',').ToList())
                                {
                                    int intCharge;
                                    if (int.TryParse(strCharge, out intCharge))
                                    {
                                        mChargeFilterList.Add(intCharge);
                                    }
                                    else
                                    {
                                        ShowErrorMessage("Invalid charge specified: " + strCharge);
                                        Console.WriteLine();
                                        return false;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowErrorMessage("Error parsing the list of charges \"" + strValue + "\"; should be a command separated list");
                            Console.WriteLine();
                            return false;
                        }
                    }

                    if (commandLineParser.RetrieveValueForParameter("P", out strValue))
                        mParameterFilePath = strValue;
                    if (commandLineParser.IsParameterPresent("NoMods"))
                        mLoadModsAndSeqInfo = false;
                    if (commandLineParser.IsParameterPresent("NoMSGF"))
                        mLoadMSGFResults = false;
                    if (commandLineParser.IsParameterPresent("NoScanStats"))
                        mLoadScanStats = false;
                    if (commandLineParser.IsParameterPresent("Preview"))
                        mPreview = true;
                    if (commandLineParser.RetrieveValueForParameter("S", out strValue))
                    {
                        mRecurseFolders = true;
                        if (!int.TryParse(strValue, out mRecurseFoldersMaxLevels))
                        {
                            mRecurseFoldersMaxLevels = 0;
                        }
                    }

                    if (commandLineParser.RetrieveValueForParameter("A", out strValue))
                        mOutputFolderAlternatePath = strValue;
                    if (commandLineParser.IsParameterPresent("R"))
                        mRecreateFolderHierarchyInAlternatePath = true;
                    if (commandLineParser.IsParameterPresent("L"))
                        mLogMessagesToFile = true;
                    return true;
                }
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
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("This program reads a tab-delimited text file created by the Peptide Hit Results Processor (PHRP) and " + "creates a PepXML with the appropriate information.  The various _SeqInfo files created by PHRP must be present in the same folder as the text file. " + "If the MASIC Scan Stats file is also present, then elution time information will be extracted and included in the PepXML file. " + "You should ideally also include the name of the parameter file used for the MS/MS search engine."));
                Console.WriteLine();
                Console.WriteLine("Program syntax:");
                Console.WriteLine(Path.GetFileName(Assembly.GetExecutingAssembly().Location) + " /I:PHRPResultsFile [/O:OutputFolderPath]");
                Console.WriteLine(" [/E:SearchEngineParamFileName] [/F:FastaFilePath] [/P:ParameterFilePath]");
                Console.WriteLine(" [/H:HitsPerSpectrum] [/X] [/TopHitOnly] [/MaxProteins:" + clsPeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM + "]");
                Console.WriteLine(" [/PepFilter:PeptideFilterFilePath] [/ChargeFilter:ChargeList]");
                Console.WriteLine(" [/NoMods] [/NoMSGF] [/NoScanStats] [/Preview]");
                Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L] [/Q]");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("The input file path can contain the wildcard character * and should point to a tab-delimited text file created by PHRP (for example, Dataset_syn.txt, Dataset_xt.txt, Dataset_msgfplus_syn.txt or Dataset_inspect_syn.txt) " + "The output folder switch is optional.  If omitted, the output file will be created in the same folder as the input file. "));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /E to specify the name of the parameter file used by the MS/MS search engine (must be in the same folder as the PHRP results file).  For X!Tandem results, the default_input.xml and taxonomy.xml files must also be present in the input folder."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /F to specify the path to the fasta file to store in the PepXML file; ignored if /E is provided and the search engine parameter file defines the fasta file to search (this is the case for SEQUEST and X!Tandem but not Inspect or MSGF+)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /H to specify the number of matches per spectrum to store (default is " + clsPeptideListToXML.DEFAULT_HITS_PER_SPECTRUM + "; use 0 to keep all hits)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /X to specify that peptides with X residues should be skipped"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /TopHitOnly to specify that each scan should only include a single peptide match (regardless of charge)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /MaxProteins to define the maximum number of proteins to track for each PSM (default is " + clsPeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM + ")"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /PepFilter:File to use a text file to filter the peptides included in the output file (one peptide sequence per line)"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /ChargeFilter:ChargeList to specify one or more charges to filter on. Examples:"));
                Console.WriteLine("  Only 2+ peptides:    /ChargeFilter:2");
                Console.WriteLine("  2+ and 3+ peptides:  /ChargeFilter:2,3");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the _ModSummary file and SeqInfo files are loaded and used to determine the modified residues; use /NoMods to skip these files"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the _MSGF.txt file is loaded to associated MSGF SpecProb values with the results; use /NoMSGF to skip this file"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the MASIC _ScanStats.txt and _ScanStatsEx.txt files are loaded to determine elution times for scan numbers; use /NoScanStats to skip these files"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /Preview to preview the files that would be required for the specified dataset (taking into account the other command line switches used)"));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /P to specific a parameter file to use.  Options in this file will override options specified for /E, /F, /H, and /X"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /S to process all valid files in the input directory and subdirectories. Include a number after /S (like /S:2) to limit the level of subdirectories to examine. " + "When using /S, you can redirect the output of the results using /A. " + "When using /S, you can use /R to re-create the input folder hierarchy in the alternate output folder (if defined)."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /L to log messages to a file.  If /Q is used, then no messages will be displayed at the console."));
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