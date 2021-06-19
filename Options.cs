using System.Collections.Generic;
using PHRPReader;

namespace PeptideListToXML
{
    /// <summary>
    /// PeptideListToXML Options
    /// </summary>
    public class Options
    {
        // Future enum if support for mzIdentML is added
        // public enum PeptideListOutputFormat
        // {
        //     PepXML = 0,
        //     mzIdentML = 1
        // }

        /// <summary>
        /// List of charge states to filter on (only storing the listed charge states)
        /// </summary>
        /// <remarks>If an empty list, return all charges</remarks>
        public List<int> ChargeFilterList { get; } = new();

        /// <summary>
        /// Dataset name
        /// </summary>
        /// <remarks>
        /// If an empty string, will be auto-determined by the PHRP Reader class in ConvertPHRPDataToXML()
        /// </remarks>
        public string DatasetName { get; set; }

        /// <summary>
        /// FASTA file path to store in the pepXML file
        /// </summary>
        /// <remarks>
        /// Ignored if the Search Engine Param File exists and it contains a fasta file name (typically the case for SEQUEST and X!Tandem)
        /// </remarks>
        public string FastaFilePath { get; set; }

        /// <summary>
        /// Input file path
        /// </summary>
        public string InputFilePath { get; set; }

        /// <summary>
        /// When true, load additional PHRP files
        /// </summary>
        public bool LoadModsAndSeqInfo { get; set; }

        /// <summary>
        /// When true, load MSGF results
        /// </summary>
        public bool LoadMSGFResults { get; set; }

        /// <summary>
        /// When true, read data from a MASIC scan stats file
        /// </summary>
        public bool LoadScanStats { get; set; }

        /// <summary>
        /// When true, log messages to a file
        /// </summary>
        public bool LogMessagesToFile { get; set; }

        // Unused: private string LogFilePath { get; set; }
        // Unused: private string LogDirectoryPath { get; set; }

        /// <summary>
        /// Maximum number of proteins per PSM to store
        /// </summary>
        public int MaxProteinsPerPSM { get; set; }

        /// <summary>
        /// Output directory path
        /// </summary>
        /// <remarks>Optional</remarks>
        public string OutputDirectoryPath { get; set; }

        // Future property if support for mzIdentML is added
        // private PeptideListOutputFormat OutputFormat { get; set; }

        /// <summary>
        /// Parameter file path
        /// </summary>
        /// <remarks>Optional</remarks>
        public string ParameterFilePath { get; set; }

        /// <summary>
        /// Peptide filter path
        /// </summary>
        /// <remarks>
        /// <para>
        /// File with a list of peptides to filter on (for inclusion in the output file); one peptide per line
        /// </para>
        /// <para>
        /// Can be a tab-delimited text file; only the first column will be used
        /// </para>
        /// </remarks>
        public string PeptideFilterFilePath { get; set; }

        /// <summary>
        /// This will be auto-determined when CachePHRPData() is called
        /// </summary>
        public PeptideHitResultTypes PeptideHitResultType { get; set; }

        /// <summary>
        /// If true, displays the names of the files that are required to create the PepXML file for the specified dataset
        /// </summary>
        public bool PreviewMode { get; set; }

        /// <summary>
        /// PSMs per spectrum to store
        /// </summary>
        /// <remarks>0 means to store all PSMs</remarks>
        public int PSMsPerSpectrumToStore { get; set; }

        /// <summary>
        /// Name of the parameter file used by the search engine that produced the input results file
        /// </summary>
        /// <remarks>Must be in the same directory as the input file</remarks>
        public string SearchEngineParamFileName { get; set; }

        /// <summary>
        /// When true, skip storing PSMs that contain an X residue
        /// </summary>
        public bool SkipXPeptides { get; set; }

        /// <summary>
        /// If True, only keep the top-scoring peptide for each scan number
        /// </summary>
        /// <remarks>If the scan has multiple charges, the output file will still only have one peptide listed for that scan number</remarks>
        public bool TopHitOnly { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Options()
        {
            ChargeFilterList.Clear();
            DatasetName = "Unknown";
            FastaFilePath = string.Empty;
            InputFilePath = string.Empty;
            LoadModsAndSeqInfo = true;
            LoadMSGFResults = true;
            LoadScanStats = true;
            LogMessagesToFile = false;
            MaxProteinsPerPSM = 100;
            OutputDirectoryPath = string.Empty;
            ParameterFilePath = string.Empty;
            PeptideFilterFilePath = string.Empty;
            PeptideHitResultType = PeptideHitResultTypes.Unknown;
            PreviewMode = false;
            PSMsPerSpectrumToStore = 3;
            SearchEngineParamFileName = string.Empty;
            SkipXPeptides = false;
            TopHitOnly = false;
        }
    }
}
