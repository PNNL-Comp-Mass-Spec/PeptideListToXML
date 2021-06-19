using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PHRPReader;
using PHRPReader.Data;
using PHRPReader.Reader;
using PRISM;
using PRISM.FileProcessor;

namespace PeptideListToXML
{
    /// <summary>
    /// This class will reads a tab-delimited text file with peptides and scores
    /// and creates a new PepXML file with the data
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe in 2012 for the Department of Energy (PNNL, Richland, WA)
    /// Converted to C# in 2021
    /// </remarks>
    public class PeptideListToXML : ProcessFilesBase
    {
        // Ignore Spelling: mzIdentML, Wiff

        /// <summary>
        /// PeptideListToXML section name in the parameter file
        /// </summary>
        public const string XML_SECTION_OPTIONS = "PeptideListToXMLOptions";

        /// <summary>
        /// Default maximum number of PSMs to store for each spectrum
        /// </summary>
        public const int DEFAULT_HITS_PER_SPECTRUM = 3;

        /// <summary>
        /// Default maximum number of proteins to store for each PSM
        /// </summary>
        public const int DEFAULT_MAX_PROTEINS_PER_PSM = 100;

        private const int PREVIEW_PAD_WIDTH = 22;

        /// <summary>
        /// Error codes specialized for this class
        /// </summary>
        public enum PeptideListToXMLErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Error reading the input file
            /// </summary>
            ErrorReadingInputFile = 1,

            /// <summary>
            /// Error writing the output file
            /// </summary>
            ErrorWritingOutputFile = 2,

            /// <summary>
            /// Mod summary file not found
            /// </summary>
            ModSummaryFileNotFound = 3,

            /// <summary>
            /// Sequence info file not found
            /// </summary>
            SeqInfoFileNotFound = 4,

            /// <summary>
            /// MSGF file not found
            /// </summary>
            MSGFFileNotFound = 5,

            /// <summary>
            /// Scan stats file not found
            /// </summary>
            ScanStatsFileNotFound = 6,

            /// <summary>
            /// Unspecified error
            /// </summary>
            UnspecifiedError = -1
        }

        private readonly Options mOptions;

        private ReaderFactory mPHRPReader;
        private PepXMLWriter mXMLWriter;

        private SortedList<int, List<ProteinInfo>> mSeqToProteinMapCached;

        // This dictionary tracks the PSMs (hits) for each spectrum
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        private Dictionary<string, List<PSM>> mPSMsBySpectrumKey;

        // This dictionary tracks the spectrum info
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        private Dictionary<string, SpectrumInfo> mSpectrumInfo;

        /// <summary>
        /// Local error code
        /// </summary>
        public PeptideListToXMLErrorCodes LocalErrorCode { get; private set; }

        // Possible future property if support for mzIdentML is added
        // public PeptideListOutputFormat OutputFormat { get; set; }

        /// <summary>
        /// Create a PepXML file using the peptides in file inputFilePath
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool ConvertPHRPDataToXML(string inputFilePath, string outputDirectoryPath)
        {
            var success = CachePHRPData(inputFilePath, out var searchEngineParams);

            if (!success)
                return false;

            if (mOptions.PreviewMode)
            {
                PreviewRequiredFiles(inputFilePath, mOptions);
                return true;
            }

            var outputFilePath = Path.Combine(outputDirectoryPath, mOptions.DatasetName + ".pepXML");

            return WriteCachedData(outputFilePath, searchEngineParams);
        }

        private bool CachePHRPData(string inputFilePath, out SearchEngineParameters searchEngineParams)
        {
            try
            {
                if (mOptions.PreviewMode)
                {
                    ResetProgress("Finding required files");
                }
                else
                {
                    ResetProgress("Caching PHRP data");
                }

                if (mPSMsBySpectrumKey is null)
                {
                    mPSMsBySpectrumKey = new Dictionary<string, List<PSM>>();
                }
                else
                {
                    mPSMsBySpectrumKey.Clear();
                }

                if (mSpectrumInfo is null)
                {
                    mSpectrumInfo = new Dictionary<string, SpectrumInfo>();
                }
                else
                {
                    mSpectrumInfo.Clear();
                }

                // Keys in this dictionary are scan numbers
                var bestPSMByScan = new Dictionary<int, PSMInfo>();

                var peptidesStored = 0;
                var startupOptions = new StartupOptions
                {
                    LoadModsAndSeqInfo = mOptions.LoadModsAndSeqInfo,
                    LoadMSGFResults = mOptions.LoadMSGFResults,
                    LoadScanStatsData = mOptions.LoadScanStats,
                    MaxProteinsPerPSM = mOptions.MaxProteinsPerPSM
                };

                mPHRPReader = new ReaderFactory(inputFilePath, startupOptions);
                RegisterEvents(mPHRPReader);

                mOptions.DatasetName = mPHRPReader.DatasetName;
                mOptions.PeptideHitResultType = mPHRPReader.PeptideHitResultType;
                mSeqToProteinMapCached = mPHRPReader.SeqToProteinMap;

                SortedSet<string> peptidesToFilterOn;
                if (!string.IsNullOrWhiteSpace(mOptions.PeptideFilterFilePath))
                {
                    var success = LoadPeptideFilterFile(mOptions.PeptideFilterFilePath, out peptidesToFilterOn);
                    if (!success)
                    {
                        searchEngineParams = new SearchEngineParameters(string.Empty);
                        return false;
                    }
                }
                else
                {
                    peptidesToFilterOn = new SortedSet<string>();
                }

                if (mOptions.PreviewMode)
                {
                    // We can exit this function now since we have determined the dataset name and peptide hit result type
                    searchEngineParams = new SearchEngineParameters(string.Empty);
                    return true;
                }

                // Report any errors cached during instantiation of mPHRPReader
                foreach (var message in mPHRPReader.ErrorMessages.Distinct())
                {
                    ShowErrorMessage(message);
                }

                // Report any warnings cached during instantiation of mPHRPReader
                foreach (var message in mPHRPReader.WarningMessages.Distinct())
                {
                    Console.WriteLine();
                    ShowWarning(message);
                    if (message.Contains("SeqInfo file not found"))
                    {
                        if (mPHRPReader.ModSummaryFileLoaded)
                        {
                            ShowMessage("  ... will use the ModSummary file to infer the peptide modifications");
                        }
                        else
                        {
                            ShowMessage("  ... use the /NoMods switch to avoid this error (though modified peptides in that case modified peptides would not be stored properly)");
                        }
                    }
                    else if (message.Contains("MSGF file not found"))
                    {
                        ShowMessage("  ... use the /NoMSGF switch to avoid this error");
                    }
                    else if (message.Contains("Extended ScanStats file not found"))
                    {
                        ShowMessage("  ... parent ion m/z values may not be completely accurate; use the /NoScanStats switch to avoid this error");
                    }
                    else if (message.Contains("ScanStats file not found"))
                    {
                        ShowMessage("  ... unable to determine elution times; use the /NoScanStats switch to avoid this error");
                    }
                    else if (message.Contains("ModSummary file not found"))
                    {
                        ShowMessage("  ... ModSummary file was not found; will infer modification details");
                    }
                }

                if (mPHRPReader.WarningMessages.Count > 0)
                    Console.WriteLine();

                mPHRPReader.ClearErrors();
                mPHRPReader.ClearWarnings();

                if (string.IsNullOrEmpty(mOptions.DatasetName))
                {
                    mOptions.DatasetName = "Unknown";
                    ShowWarning("Unable to determine the dataset name from the input file path; database will be named " + mOptions.DatasetName + " in the PepXML file");
                }

                while (mPHRPReader.MoveNext())
                {
                    var currentPSM = mPHRPReader.CurrentPSM;

                    var skipPeptide = mOptions.SkipXPeptides && currentPSM.PeptideCleanSequence.Contains("X");

                    if (!skipPeptide && mOptions.PSMsPerSpectrumToStore > 0 && currentPSM.ScoreRank > mOptions.PSMsPerSpectrumToStore)
                    {
                        skipPeptide = true;
                    }

                    if (!skipPeptide && peptidesToFilterOn.Count > 0 && !peptidesToFilterOn.Contains(currentPSM.PeptideCleanSequence))
                    {
                        skipPeptide = true;
                    }

                    if (!skipPeptide && mOptions.ChargeFilterList.Count > 0 && !mOptions.ChargeFilterList.Contains(currentPSM.Charge))
                    {
                        skipPeptide = true;
                    }

                    if (skipPeptide)
                    {
                        continue;
                    }

                    var spectrumKey = GetSpectrumKey(currentPSM);
                    if (!mSpectrumInfo.ContainsKey(spectrumKey))
                    {
                        // New spectrum; add a new entry to mSpectrumInfo
                        var spectrumInfo = new SpectrumInfo(spectrumKey)
                        {
                            StartScan = currentPSM.ScanNumberStart,
                            EndScan = currentPSM.ScanNumberEnd,
                            PrecursorNeutralMass = currentPSM.PrecursorNeutralMass,
                            AssumedCharge = currentPSM.Charge,
                            ElutionTimeMinutes = currentPSM.ElutionTimeMinutes,
                            CollisionMode = currentPSM.CollisionMode,
                            Index = mSpectrumInfo.Count,
                            NativeID = ConstructNativeID(currentPSM.ScanNumberStart)
                        };

                        mSpectrumInfo.Add(spectrumKey, spectrumInfo);
                    }

                    if (mPSMsBySpectrumKey.TryGetValue(spectrumKey, out var psms))
                    {
                        psms.Add(currentPSM);
                    }
                    else
                    {
                        psms = new List<PSM>
                        {
                            currentPSM
                        };

                        mPSMsBySpectrumKey.Add(spectrumKey, psms);
                    }

                    if (mOptions.TopHitOnly)
                    {
                        var comparisonPSMInfo = new PSMInfo(spectrumKey, currentPSM);

                        if (bestPSMByScan.TryGetValue(currentPSM.ScanNumberStart, out var bestPSMInfo))
                        {
                            if (comparisonPSMInfo.MSGFSpecProb < bestPSMInfo.MSGFSpecProb)
                            {
                                // We have found a better scoring peptide for this scan
                                bestPSMByScan[currentPSM.ScanNumberStart] = comparisonPSMInfo;
                            }
                        }
                        else
                        {
                            bestPSMByScan.Add(currentPSM.ScanNumberStart, comparisonPSMInfo);
                        }
                    }

                    peptidesStored++;

                    UpdateProgress(mPHRPReader.PercentComplete);
                }

                OperationComplete();
                Console.WriteLine();
                var filterMessage = string.Empty;
                if (peptidesToFilterOn.Count > 0)
                {
                    filterMessage = " (filtered using " + peptidesToFilterOn.Count + " peptides in " + Path.GetFileName(mOptions.PeptideFilterFilePath) + ")";
                }

                if (mOptions.TopHitOnly)
                {
                    // Update mPSMsBySpectrumKey to contain the best hit for each scan number (regardless of charge)

                    var countAtStart = mPSMsBySpectrumKey.Count;
                    mPSMsBySpectrumKey.Clear();
                    foreach (var item in bestPSMByScan)
                    {
                        var psms = new List<PSM> { item.Value.PSM };
                        mPSMsBySpectrumKey.Add(item.Value.SpectrumKey, psms);
                    }

                    peptidesStored = mPSMsBySpectrumKey.Count;
                    ShowMessage(" ... cached " + peptidesStored.ToString("#,##0") + " PSMs" + filterMessage);
                    ShowMessage(" ... filtered out " + (countAtStart - peptidesStored).ToString("#,##0") + " PSMs to only retain the top hit for each scan (regardless of charge)");
                }
                else
                {
                    ShowMessage(" ... cached " + peptidesStored.ToString("#,##0") + " PSMs" + filterMessage);
                }

                // Load the search engine parameters
                searchEngineParams = LoadSearchEngineParameters(mPHRPReader, mOptions.SearchEngineParamFileName);
                return true;
            }
            catch (Exception ex)
            {
                if (mOptions.PreviewMode)
                {
                    Console.WriteLine();
                    ShowMessage("Unable to preview the required files since not able to determine the dataset name: " + ex.Message);
                }
                else
                {
                    ShowErrorMessage("Error Reading source file in CachePHRPData: " + ex.Message);
                    if (ex.Message.Contains("ModSummary file not found"))
                    {
                        SetLocalErrorCode(PeptideListToXMLErrorCodes.ModSummaryFileNotFound);
                    }
                    else
                    {
                        SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile);
                        ShowMessage(ex.StackTrace);
                    }
                }

                searchEngineParams = new SearchEngineParameters(string.Empty);
                return false;
            }
        }

        /// <summary>
        /// Constructs a Thermo-style nativeID string for the given spectrum
        /// This allows for linking up with data in .mzML files
        /// </summary>
        /// <param name="scanNumber"></param>
        private string ConstructNativeID(int scanNumber)
        {
            // Examples:
            // Most Thermo raw files: "controllerType=0 controllerNumber=1 scan=6"
            // Thermo raw with PQD spectra: "controllerType=1 controllerNumber=1 scan=6"
            // Wiff files: "sample=1 period=1 cycle=123 experiment=2"
            // Waters files: "function=2 process=0 scan=123

            // For now, we're assuming all data processed by this program is from Thermo raw files

            return "controllerType=0 controllerNumber=1 scan=" + scanNumber;
        }

        /// <summary>
        /// Get the default file extensions that this class knows how to parse
        /// </summary>
        public override IList<string> GetDefaultExtensionsToParse()
        {
            return new List<string>
            {
                InspectSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                MaxQuantSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                MODaSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                MODPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                MSGFPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                SequestSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                XTandemSynFileReader.GetPHRPSynopsisFileName(string.Empty)
            };
        }

        /// <summary>
        /// Get the error message; empty string if no error
        /// </summary>
        public override string GetErrorMessage()
        {
            if (ErrorCode is ProcessFilesErrorCodes.LocalizedError or ProcessFilesErrorCodes.NoError)
            {
                return LocalErrorCode switch
                {
                    PeptideListToXMLErrorCodes.NoError => string.Empty,
                    PeptideListToXMLErrorCodes.ErrorReadingInputFile =>
                        "Error reading input file",
                    PeptideListToXMLErrorCodes.ErrorWritingOutputFile =>
                        "Error writing to the output file",
                    PeptideListToXMLErrorCodes.ModSummaryFileNotFound =>
                        "ModSummary file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)",
                    PeptideListToXMLErrorCodes.SeqInfoFileNotFound =>
                        "SeqInfo file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)",
                    PeptideListToXMLErrorCodes.MSGFFileNotFound =>
                        "MSGF file not found; use the /NoMSGF switch to silence this error",
                    PeptideListToXMLErrorCodes.ScanStatsFileNotFound =>
                        "MASIC ScanStats file not found; use the /NoScanStats switch to avoid this error",
                    PeptideListToXMLErrorCodes.UnspecifiedError =>
                        "Unspecified localized error",
                    _ => "Unknown error state"
                };
            }

            return GetBaseClassErrorMessage();
        }

        private string GetSpectrumKey(PSM CurrentPSM)
        {
            return mOptions.DatasetName + "." + CurrentPSM.ScanNumberStart + "." + CurrentPSM.ScanNumberEnd + "." + CurrentPSM.Charge;
        }

        /// <summary>
        /// Loads the settings from the parameter file
        /// </summary>
        /// <param name="parameterFilePath"></param>
        /// <returns>True if successful (or no parameter file is defined); false if an error</returns>
        public bool LoadParameterFileSettings(string parameterFilePath)
        {
            var settingsFile = new XmlSettingsFileAccessor();
            try
            {
                if (string.IsNullOrWhiteSpace(parameterFilePath))
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (settingsFile.LoadSettings(parameterFilePath))
                {
                    if (!settingsFile.SectionPresent(XML_SECTION_OPTIONS))
                    {
                        ShowErrorMessage("The node '<section name=\"" + XML_SECTION_OPTIONS + "\"> was not found in the parameter file: " + parameterFilePath);
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                        return false;
                    }

                    mOptions.FastaFilePath = settingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", mOptions.FastaFilePath);
                    mOptions.SearchEngineParamFileName = settingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", mOptions.SearchEngineParamFileName);
                    mOptions.PSMsPerSpectrumToStore = settingsFile.GetParam(XML_SECTION_OPTIONS, "PSMsPerSpectrumToStore", mOptions.PSMsPerSpectrumToStore);
                    mOptions.SkipXPeptides = settingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", mOptions.SkipXPeptides);
                    mOptions.TopHitOnly = settingsFile.GetParam(XML_SECTION_OPTIONS, "TopHitOnly", mOptions.TopHitOnly);
                    mOptions.MaxProteinsPerPSM = settingsFile.GetParam(XML_SECTION_OPTIONS, "MaxProteinsPerPSM", mOptions.MaxProteinsPerPSM);
                    mOptions.LoadModsAndSeqInfo = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadModsAndSeqInfo", mOptions.LoadModsAndSeqInfo);
                    mOptions.LoadMSGFResults = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadMSGFResults", mOptions.LoadMSGFResults);
                    mOptions.LoadScanStats = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadScanStats", mOptions.LoadScanStats);
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;
        }

        private bool LoadPeptideFilterFile(string inputFilePath, out SortedSet<string> peptides)
        {
            peptides = new SortedSet<string>();

            try
            {
                if (!File.Exists(inputFilePath))
                {
                    ShowErrorMessage("Peptide filter file not found: " + inputFilePath);
                    SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile);
                    return false;
                }

                if (mOptions.PreviewMode)
                {
                    ShowMessage("Peptide Filter file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetMSGFFileName(inputFilePath));
                    return true;
                }

                using var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(dataLine))
                    {
                        // Remove any text present after a tab character
                        var tabIndex = dataLine.IndexOf('\t');
                        if (tabIndex > 0)
                        {
                            dataLine = dataLine.Substring(0, tabIndex);
                        }

                        if (!string.IsNullOrWhiteSpace(dataLine))
                        {
                            dataLine = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(dataLine, true);
                            peptides.Add(dataLine);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadPeptideFilterFile", ex);
                return false;
            }

            return true;
        }

        private SearchEngineParameters LoadSearchEngineParameters(ReaderFactory reader, string searchEngineParamFileName)
        {
            SearchEngineParameters searchEngineParams = null;

            try
            {
                Console.WriteLine();
                if (string.IsNullOrEmpty(searchEngineParamFileName))
                {
                    ShowWarning("Search engine parameter file not defined; use /E to specify the filename");
                    searchEngineParams = new SearchEngineParameters(mOptions.PeptideHitResultType.ToString());
                }
                else
                {
                    ShowMessage("Loading Search Engine parameters");
                    var success = reader.SynFileReader.LoadSearchEngineParameters(searchEngineParamFileName, out searchEngineParams);

                    if (!success)
                    {
                        searchEngineParams = new SearchEngineParameters(mOptions.PeptideHitResultType.ToString());
                    }
                }

                // Make sure mSearchEngineParams.ModInfo is up-to-date

                foreach (var item in mPSMsBySpectrumKey)
                {
                    var spectrumKey = item.Key;
                    if (!mSpectrumInfo.ContainsKey(spectrumKey))
                    {
                        continue;
                    }

                    foreach (var psmEntry in item.Value)
                    {
                        if (psmEntry.ModifiedResidues.Count == 0)
                        {
                            continue;
                        }

                        foreach (var residue in psmEntry.ModifiedResidues)
                        {
                            // Check whether residue.ModDefinition is present in searchEngineParams.ModInfo
                            var matchFound = false;
                            foreach (var knownMod in searchEngineParams.ModList)
                            {
                                if (knownMod.EquivalentMassTypeTagAtomAndResidues(residue.ModDefinition))
                                {
                                    matchFound = true;
                                    break;
                                }
                            }

                            if (!matchFound)
                            {
                                searchEngineParams.ModList.Add(residue.ModDefinition);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadSearchEngineParameters", ex);
            }

            return searchEngineParams;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public PeptideListToXML(Options options)
        {
            mFileDate = "June 17, 2021";
            mOptions = options;

            LocalErrorCode = PeptideListToXMLErrorCodes.NoError;
        }

        private void PreviewRequiredFiles(string inputFilePath, Options options)
        {
            var inputFile = new FileInfo(inputFilePath);

            if (inputFile.DirectoryName == null)
            {
                ShowWarning("Unable to determine the parent directory of the input file");
                return;
            }

            Console.WriteLine();
            ShowMessage("Data file directory: " + PathUtils.CompactPathString(inputFile.DirectoryName, 110));

            ShowMessage("Data file: ".PadRight(PREVIEW_PAD_WIDTH) + Path.GetFileName(inputFilePath));
            if (mOptions.LoadModsAndSeqInfo)
            {
                ShowMessage("ModSummary file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetPHRPModSummaryFileName(mOptions.PeptideHitResultType, mOptions.DatasetName));
                if (inputFile.Name.Equals(ReaderFactory.GetPHRPSynopsisFileName(mOptions.PeptideHitResultType, mOptions.DatasetName), StringComparison.OrdinalIgnoreCase))
                {
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetPHRPResultToSeqMapFileName(mOptions.PeptideHitResultType, mOptions.DatasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetPHRPSeqInfoFileName(mOptions.PeptideHitResultType, mOptions.DatasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetPHRPSeqToProteinMapFileName(mOptions.PeptideHitResultType, mOptions.DatasetName));
                }
            }

            if (options.LoadMSGFResults)
            {
                ShowMessage("MSGF Results file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetMSGFFileName(inputFilePath));
            }

            if (options.LoadScanStats)
            {
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetScanStatsFilename(options.DatasetName));
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetExtendedScanStatsFilename(options.DatasetName));
            }

            if (string.IsNullOrEmpty(options.SearchEngineParamFileName))
            {
                return;
            }

            ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + options.SearchEngineParamFileName);
            ShowMessage("Tool Version File: ".PadRight(PREVIEW_PAD_WIDTH) + ReaderFactory.GetToolVersionInfoFilenames(options.PeptideHitResultType).FirstOrDefault());

            if (options.PeptideHitResultType != PeptideHitResultTypes.XTandem)
            {
                return;
            }

            // Determine the additional files that will be required for X!Tandem
            foreach (var fileName in XTandemSynFileReader.GetAdditionalSearchEngineParamFileNames(
                Path.Combine(inputFile.DirectoryName, options.SearchEngineParamFileName)))
            {
                ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + fileName);
            }
        }

        /// <summary>
        /// Main processing function; calls ConvertPHRPDataToXML
        /// </summary>
        /// <param name="inputFilePath">PHRP Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path (if empty, the output file will be created in the same directory as the input file)</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">True to reset the error code prior to processing</param>
        /// <returns>True if successful, false if an error</returns>
        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
            if (resetErrorCode)
            {
                SetLocalErrorCode(PeptideListToXMLErrorCodes.NoError);
            }

            if (!LoadParameterFileSettings(parameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + parameterFilePath);
                if (ErrorCode == ProcessFilesErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                }

                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    ShowMessage("Input file name is empty");
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                    return false;
                }

                Console.WriteLine();
                if (!mOptions.PreviewMode)
                {
                    ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
                }

                // Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                if (!CleanupFilePaths(ref inputFilePath, ref outputDirectoryPath))
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError);
                }
                else
                {
                    if (!mOptions.PreviewMode)
                    {
                        ResetProgress();
                    }

                    try
                    {
                        // Obtain the full path to the input file
                        var inputFile = new FileInfo(inputFilePath);
                        var inputFilePathFull = inputFile.FullName;
                        return ConvertPHRPDataToXML(inputFilePathFull, outputDirectoryPath);
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error calling ConvertToXML", ex);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFile", ex);
                return false;
            }
        }

        private void SetLocalErrorCode(PeptideListToXMLErrorCodes newErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && LocalErrorCode != PeptideListToXMLErrorCodes.NoError)
            {
                // An error code is already defined; do not change it
                return;
            }

            LocalErrorCode = newErrorCode;
            if (newErrorCode == PeptideListToXMLErrorCodes.NoError)
            {
                if (ErrorCode == ProcessFilesErrorCodes.LocalizedError)
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError);
                }
            }
            else
            {
                SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError);
            }
        }

        private bool WriteCachedData(string outputFilePath, SearchEngineParameters searchEngineParams)
        {
            ResetProgress("Creating the .pepXML file");
            try
            {
                Console.WriteLine();
                ShowMessage("Creating PepXML file at " + Path.GetFileName(outputFilePath));
                mXMLWriter = new PepXMLWriter(outputFilePath, searchEngineParams, mOptions);
                RegisterEvents(mXMLWriter);

                var spectra = 0;
                var peptides = 0;

                foreach (var spectrumKey in mPSMsBySpectrumKey.Keys)
                {
                    var psm = mPSMsBySpectrumKey[spectrumKey];

                    if (mSpectrumInfo.TryGetValue(spectrumKey, out var currentSpectrum))
                    {
                        spectra++;
                        peptides += psm.Count;

                        mXMLWriter.WriteSpectrum(currentSpectrum, psm, mSeqToProteinMapCached);
                    }
                    else
                    {
                        ShowErrorMessage("Spectrum key '" + spectrumKey + "' not found in mSpectrumInfo; this is unexpected");
                    }

                    var pctComplete = spectra / (float)mPSMsBySpectrumKey.Count * 100f;
                    UpdateProgress(pctComplete);
                }

                if (peptides > 500)
                {
                    Console.WriteLine();
                }

                mXMLWriter.CloseDocument();
                Console.WriteLine();
                ShowMessage("PepXML file created with " + spectra.ToString("#,##0") + " spectra and " + peptides.ToString("#,##0") + " peptides");

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error Reading source file in WriteCachedData: " + ex.Message);
                return false;
            }
        }
    }
}
