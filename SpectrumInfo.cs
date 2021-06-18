namespace PeptideListToXML
{
    /// <summary>
    /// Spectrum info class
    /// </summary>
    public class SpectrumInfo
    {
        /// <summary>
        /// Spectrum Title
        /// </summary>
        /// <remarks>
        /// Examples titles:
        /// QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
        /// scan=16134 cs=2
        /// </remarks>
        public string SpectrumTitle { get; }

        /// <summary>
        /// Start scan number
        /// </summary>
        public int StartScan { get; set; }

        /// <summary>
        /// End scan number (if this is a merged spectrum)
        /// </summary>
        public int EndScan { get; set; }

        /// <summary>
        /// Monoisotopic mass of the precursor ion
        /// </summary>
        public double PrecursorNeutralMass { get; set; }

        /// <summary>
        /// Assumed charge state of the precursor ion
        /// </summary>
        public int AssumedCharge { get; set; }

        /// <summary>
        /// Elution time, in minutes
        /// </summary>
        public double ElutionTimeMinutes { get; set; }

        /// <summary>
        /// Collision mode
        /// </summary>
        public string CollisionMode { get; set; }

        /// <summary>
        /// Spectrum index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Native ID (as assigned by msconvert.exe)
        /// </summary>
        public string NativeID { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="title"></param>
        public SpectrumInfo(string title)
        {
            SpectrumTitle = title;
        }
    }
}
