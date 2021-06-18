using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PRISM;

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
    public class PeptideListToXML : PRISM.FileProcessor.ProcessFilesBase
    {
        // Ignore Spelling: mzIdentML, Wiff

        #region Constants and Enums

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

        // Possible future enum if support for mzIdentML is added
        // public enum PeptideListOutputFormat
        // {
        //     PepXML = 0,
        //     mzIdentML = 1
        // }

        /// <summary>
        /// Error codes specialized for this class
        /// </summary>
        public enum PeptideListToXMLErrorCodes
        {
            NoError = 0,
            ErrorReadingInputFile = 1,
            ErrorWritingOutputFile = 2,
            ModSummaryFileNotFound = 3,
            SeqInfoFileNotFound = 4,
            MSGFStatsFileNotFound = 5,
            ScanStatsFileNotFound = 6,
            UnspecifiedError = -1
        }

        #endregion

        #region Structures

        #endregion

        #region Class wide Variables

        // Future enum; mzIdentML is not yet supported
        // private mOutputFormat As clsPeptideListToXML.PeptideListOutputFormat

        private PHRPReader.ReaderFactory mPHRPReader;
        private PepXMLWriter mXMLWriter;

        private PHRPReader.PeptideHitResultTypes mPeptideHitResultType;
        private SortedList<int, List<PHRPReader.Data.ProteinInfo>> mSeqToProteinMapCached;

        private string mPeptideFilterFilePath;

        // This dictionary tracks the PSMs (hits) for each spectrum
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        private Dictionary<string, List<PHRPReader.Data.PSM>> mPSMsBySpectrumKey;

        // This dictionary tracks the spectrum info
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        private Dictionary<string, PepXMLWriter.SpectrumInfoType> mSpectrumInfo;

        #endregion

        #region Processing Options Interface Functions

        /// <summary>
        /// Charge filter list: list of charges to include in the output file
        /// </summary>
        /// <remarks>If an empty list, return all charges</remarks>
        public List<int> ChargeFilterList { get; } = new();

        /// <summary>
        /// Dataset name; auto-determined by the PHRP Reader class in ConvertPHRPDataToXML()
        /// </summary>
        public string DatasetName { get; private set; }

        /// <summary>
        /// Fasta file path to store in the pepXML file
        /// Ignored if the Search Engine Param File exists and it contains a fasta file name (typically the case for SEQUEST and X!Tandem)
        /// </summary>
        public string FastaFilePath { get; set; }

        /// <summary>
        /// Number of peptides per spectrum to store in the PepXML file; 0 means store all hits
        /// </summary>
        public int HitsPerSpectrum { get; set; }

        /// <summary>
        /// When true, load addition PHRP files
        /// </summary>
        public bool LoadModsAndSeqInfo { get; set; }

        /// <summary>
        /// When true, load MSGF results
        /// </summary>
        public bool LoadMSGFResults { get; set; }

        /// <summary>
        /// When true, load Scan Stats files
        /// </summary>
        public bool LoadScanStats { get; set; }

        /// <summary>
        /// Local error code
        /// </summary>
        public PeptideListToXMLErrorCodes LocalErrorCode { get; private set; }

        /// <summary>
        /// Maximum number of proteins to store for each PSM
        /// </summary>
        public int MaxProteinsPerPSM { get; set; }

        /// <summary>
        /// If true, displays the names of the files that are required to create the PepXML file for the specified dataset
        /// </summary>
        public bool PreviewMode { get; set; }

        /// <summary>
        /// Name of the parameter file used by the search engine that produced the results file that we are parsing
        /// </summary>
        public string SearchEngineParamFileName { get; set; }

        /// <summary>
        /// If True, skip peptides with X residues
        /// </summary>
        public bool SkipXPeptides { get; set; }

        /// <summary>
        /// If True, only keep the top-scoring peptide for each scan number
        /// </summary>
        /// <remarks>If the scan has multiple charges, the output file will still only have one peptide listed for that scan number</remarks>
        public bool TopHitOnly { get; set; }

        /// <summary>
        /// Peptide filter file path
        /// </summary>
        /// <remarks>
        /// File with a list of peptides to filter on (for inclusion in the output file)
        /// One peptide per line
        /// </remarks>
        public string PeptideFilterFilePath
        {
            get => mPeptideFilterFilePath;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    mPeptideFilterFilePath = string.Empty;
                }
                else
                {
                    mPeptideFilterFilePath = value;
                }
            }
        }

        // Future enum; mzIdentML is not yet supported
        // Public Property OutputFormat() As PeptideListOutputFormat
        // Get
        // Return mOutputFormat
        // End Get
        // Set(value As PeptideListOutputFormat)
        // mOutputFormat = value
        // End Set
        // End Property

        #endregion

        /// <summary>
        /// Create a PepXML file using the peptides in file inputFilePath
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputFolderPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool ConvertPHRPDataToXML(string inputFilePath, string outputFolderPath)
        {
          // Note that CachePHRPData() will update these variables
            DatasetName = "Unknown";
            mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown;
            var success = CachePHRPData(inputFilePath, out var searchEngineParams);

            if (!success)
                return false;

            if (PreviewMode)
            {
                PreviewRequiredFiles(inputFilePath, DatasetName, mPeptideHitResultType, LoadModsAndSeqInfo, LoadMSGFResults, LoadScanStats, SearchEngineParamFileName);
                return true;
            }

            var outputFilePath = Path.Combine(outputFolderPath, DatasetName + ".pepXML");
            return WriteCachedData(inputFilePath, outputFilePath, searchEngineParams);
        }

        private bool CachePHRPData(string inputFilePath, out PHRPReader.Data.SearchEngineParameters searchEngineParams)
        {
            try
            {
                if (PreviewMode)
                {
                    ResetProgress("Finding required files");
                }
                else
                {
                    ResetProgress("Caching PHRP data");
                }

                if (mPSMsBySpectrumKey is null)
                {
                    mPSMsBySpectrumKey = new Dictionary<string, List<PHRPReader.Data.PSM>>();
                }
                else
                {
                    mPSMsBySpectrumKey.Clear();
                }

                if (mSpectrumInfo is null)
                {
                    mSpectrumInfo = new Dictionary<string, PepXMLWriter.SpectrumInfoType>();
                }
                else
                {
                    mSpectrumInfo.Clear();
                }

                // Keys in this dictionary are scan numbers
                var bestPSMByScan = new Dictionary<int, PSMInfo>();

                var peptidesStored = 0;
                var startupOptions = new PHRPReader.StartupOptions()
                {
                    LoadModsAndSeqInfo = LoadModsAndSeqInfo,
                    LoadMSGFResults = LoadMSGFResults,
                    LoadScanStatsData = LoadScanStats,
                    MaxProteinsPerPSM = MaxProteinsPerPSM
                };

                mPHRPReader = new PHRPReader.ReaderFactory(inputFilePath, startupOptions);
                RegisterEvents(mPHRPReader);

                DatasetName = mPHRPReader.DatasetName;
                mPeptideHitResultType = mPHRPReader.PeptideHitResultType;
                mSeqToProteinMapCached = mPHRPReader.SeqToProteinMap;

                SortedSet<string> peptidesToFilterOn;
                if (!string.IsNullOrWhiteSpace(mPeptideFilterFilePath))
                {
                    var success = LoadPeptideFilterFile(mPeptideFilterFilePath, out peptidesToFilterOn);
                    if (!success)
                    {
                        searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
                        return false;
                    }
                }
                else
                {
                    peptidesToFilterOn = new SortedSet<string>();
                }

                if (PreviewMode)
                {
                    // We can exit this function now since we have determined the dataset name and peptide hit result type
                    searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
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
                    ShowWarningMessage(message);
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

                if (string.IsNullOrEmpty(DatasetName))
                {
                    DatasetName = "Unknown";
                    ShowWarningMessage("Unable to determine the dataset name from the input file path; database will be named " + DatasetName + " in the PepXML file");
                }

                while (mPHRPReader.MoveNext())
                {
                    var currentPSM = mPHRPReader.CurrentPSM;

                    var skipPeptide = SkipXPeptides && currentPSM.PeptideCleanSequence.Contains("X");

                    if (!skipPeptide && HitsPerSpectrum > 0 && currentPSM.ScoreRank > HitsPerSpectrum)
                    {
                        skipPeptide = true;
                    }

                    if (!skipPeptide && peptidesToFilterOn.Count > 0 && !peptidesToFilterOn.Contains(currentPSM.PeptideCleanSequence))
                    {
                        skipPeptide = true;
                    }

                    if (!skipPeptide && ChargeFilterList.Count > 0 && !ChargeFilterList.Contains(currentPSM.Charge))
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
                        var spectrumInfo = new PepXMLWriter.SpectrumInfoType
                        {
                            SpectrumName = spectrumKey,
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
                        psms = new List<PHRPReader.Data.PSM>
                        {
                            currentPSM
                        };

                        mPSMsBySpectrumKey.Add(spectrumKey, psms);
                    }

                    if (TopHitOnly)
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
                    filterMessage = " (filtered using " + peptidesToFilterOn.Count + " peptides in " + Path.GetFileName(mPeptideFilterFilePath) + ")";
                }

                if (TopHitOnly)
                {
                    // Update mPSMsBySpectrumKey to contain the best hit for each scan number (regardless of charge)

                    var countAtStart = mPSMsBySpectrumKey.Count;
                    mPSMsBySpectrumKey.Clear();
                    foreach (var item in bestPSMByScan)
                    {
                        var psms = new List<PHRPReader.Data.PSM>() { item.Value.PSM };
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
                searchEngineParams = LoadSearchEngineParameters(mPHRPReader, SearchEngineParamFileName);
                return true;
            }
            catch (Exception ex)
            {
                if (PreviewMode)
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

                searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
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

            return "controllerType=0 controllerNumber=1 scan=" + scanNumber.ToString();
        }

        /// <summary>
        /// Get the default file extensions that this class knows how to parse
        /// </summary>
        public override IList<string> GetDefaultExtensionsToParse()
        {
            return new List<string>()
            {
                PHRPReader.Reader.InspectSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.MaxQuantSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.MODaSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.MODPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.MSGFPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.SequestSynFileReader.GetPHRPSynopsisFileName(string.Empty),
                PHRPReader.Reader.XTandemSynFileReader.GetPHRPSynopsisFileName(string.Empty)
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
                    PeptideListToXMLErrorCodes.MSGFStatsFileNotFound =>
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

        private string GetSpectrumKey(PHRPReader.Data.PSM CurrentPSM)
        {
            return DatasetName + "." + CurrentPSM.ScanNumberStart + "." + CurrentPSM.ScanNumberEnd + "." + CurrentPSM.Charge;
        }

        private void InitializeLocalVariables()
        {
            LocalErrorCode = PeptideListToXMLErrorCodes.NoError;
            DatasetName = "Unknown";
            mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown;
            FastaFilePath = string.Empty;
            SearchEngineParamFileName = string.Empty;
            HitsPerSpectrum = DEFAULT_HITS_PER_SPECTRUM;
            SkipXPeptides = false;
            TopHitOnly = false;
            MaxProteinsPerPSM = DEFAULT_MAX_PROTEINS_PER_PSM;
            mPeptideFilterFilePath = string.Empty;
            ChargeFilterList.Clear();
            LoadModsAndSeqInfo = true;
            LoadMSGFResults = true;
            LoadScanStats = true;
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

                    FastaFilePath = settingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", FastaFilePath);
                    SearchEngineParamFileName = settingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", SearchEngineParamFileName);
                    HitsPerSpectrum = settingsFile.GetParam(XML_SECTION_OPTIONS, "HitsPerSpectrum", HitsPerSpectrum);
                    SkipXPeptides = settingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", SkipXPeptides);
                    TopHitOnly = settingsFile.GetParam(XML_SECTION_OPTIONS, "TopHitOnly", TopHitOnly);
                    MaxProteinsPerPSM = settingsFile.GetParam(XML_SECTION_OPTIONS, "MaxProteinsPerPSM", MaxProteinsPerPSM);
                    LoadModsAndSeqInfo = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadModsAndSeqInfo", LoadModsAndSeqInfo);
                    LoadMSGFResults = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadMSGFResults", LoadMSGFResults);
                    LoadScanStats = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadScanStats", LoadScanStats);
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

                if (PreviewMode)
                {
                    ShowMessage("Peptide Filter file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(inputFilePath));
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
                            dataLine = PHRPReader.PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(dataLine, true);
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

        private PHRPReader.Data.SearchEngineParameters LoadSearchEngineParameters(PHRPReader.ReaderFactory reader, string searchEngineParamFileName)
        {
            PHRPReader.Data.SearchEngineParameters searchEngineParams = null;

            try
            {
                Console.WriteLine();
                if (string.IsNullOrEmpty(searchEngineParamFileName))
                {
                    ShowWarningMessage("Search engine parameter file not defined; use /E to specify the filename");
                    searchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
                }
                else
                {
                    ShowMessage("Loading Search Engine parameters");
                    var success = reader.SynFileReader.LoadSearchEngineParameters(searchEngineParamFileName, out searchEngineParams);

                    if (!success)
                    {
                        searchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
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
        public PeptideListToXML()
        {
            mFileDate = "June 17, 2021";
            InitializeLocalVariables();
        }

        private void PreviewRequiredFiles(string inputFilePath, string datasetName, PHRPReader.PeptideHitResultTypes PeptideHitResultTypes, bool loadModsAndSeqInfo, bool loadMSGFResults, bool loadScanStats, string searchEngineParamFileName)
        {
            var inputFile = new FileInfo(inputFilePath);
            Console.WriteLine();
            if (inputFile.DirectoryName.Length > 40)
            {
                ShowMessage("Data file folder: ");
                ShowMessage(inputFile.DirectoryName);
                Console.WriteLine();
            }
            else
            {
                ShowMessage("Data file folder: " + inputFile.DirectoryName);
            }

            ShowMessage("Data file: ".PadRight(PREVIEW_PAD_WIDTH) + Path.GetFileName(inputFilePath));
            if (loadModsAndSeqInfo)
            {
                ShowMessage("ModSummary file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPModSummaryFileName(PeptideHitResultTypes, datasetName));
                if (inputFile.Name.Equals(PHRPReader.ReaderFactory.GetPHRPSynopsisFileName(PeptideHitResultTypes, datasetName), StringComparison.OrdinalIgnoreCase))
                {
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPResultToSeqMapFileName(PeptideHitResultTypes, datasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqInfoFileName(PeptideHitResultTypes, datasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqToProteinMapFileName(PeptideHitResultTypes, datasetName));
                }
            }

            if (loadMSGFResults)
            {
                ShowMessage("MSGF Results file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(inputFilePath));
            }

            if (loadScanStats)
            {
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetScanStatsFilename(DatasetName));
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetExtendedScanStatsFilename(DatasetName));
            }

            if (string.IsNullOrEmpty(searchEngineParamFileName))
            {
                return;
            }

            ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + searchEngineParamFileName);
            ShowMessage("Tool Version File: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetToolVersionInfoFilenames(PeptideHitResultTypes).FirstOrDefault());

            if (mPeptideHitResultType != PHRPReader.PeptideHitResultTypes.XTandem)
            {
                return;
            }

            // Determine the additional files that will be required for X!Tandem
            foreach (var fileName in PHRPReader.Reader.XTandemSynFileReader.GetAdditionalSearchEngineParamFileNames(
                Path.Combine(inputFile.DirectoryName, searchEngineParamFileName)))
            {
                ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + fileName);
            }
        }

        /// <summary>
        /// Main processing function; calls ConvertPHRPDataToXML
        /// </summary>
        /// <param name="inputFilePath">PHRP Input file path</param>
        /// <param name="outputFolderPath">Output folder path (if empty, the output file will be created in the same folder as the input file)</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">True to reset the error code prior to processing</param>
        /// <returns>True if successful, false if an error</returns>
        public override bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode)
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
                if (!PreviewMode)
                {
                    ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
                }

                // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                if (!CleanupFilePaths(ref inputFilePath, ref outputFolderPath))
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError);
                }
                else
                {
                    if (!PreviewMode)
                    {
                        ResetProgress();
                    }

                    try
                    {
                        // Obtain the full path to the input file
                        var inputFile = new FileInfo(inputFilePath);
                        var inputFilePathFull = inputFile.FullName;
                        return ConvertPHRPDataToXML(inputFilePathFull, outputFolderPath);
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

        private void ShowWarningMessage(string warningMessage)
        {
            ShowMessage("Warning: " + warningMessage);
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

        private bool WriteCachedData(string inputFilePath, string outputFilePath, PHRPReader.Data.SearchEngineParameters searchEngineParams)
        {
            ResetProgress("Creating the .pepXML file");
            try
            {
                Console.WriteLine();
                ShowMessage("Creating PepXML file at " + Path.GetFileName(outputFilePath));
                mXMLWriter = new PepXMLWriter(DatasetName, FastaFilePath, searchEngineParams, inputFilePath, outputFilePath);
                RegisterEvents(mXMLWriter);

                mXMLWriter.MaxProteinsPerPSM = MaxProteinsPerPSM;
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
