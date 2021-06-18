
namespace PeptideListToXML
{
    /// <summary>
    /// PSM Info
    /// </summary>
    public class PSMInfo
    {
        /// <summary>
        /// Flag to indicate that MSGF SpecProb is not defined
        /// </summary>
        public const double MSGF_SPEC_NOT_DEFINED = 100.0;

        private readonly double mMSGFSpecProb;

        /// <summary>
        /// Peptide-sequence match
        /// </summary>
        public PHRPReader.Data.PSM PSM { get; }

        /// <summary>
        /// Spectrum key
        /// </summary>
        public string SpectrumKey { get; }

        /// <summary>
        /// MSGF SpecProb value
        /// </summary>
        public double MSGFSpecProb => mMSGFSpecProb;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="spectrumKey"></param>
        /// <param name="psm"></param>
        public PSMInfo(string spectrumKey, PHRPReader.Data.PSM psm)
        {
            SpectrumKey = spectrumKey;
            mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            PSM = psm;

            if (PSM is null)
            {
                mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            }
            else if (!double.TryParse(PSM.MSGFSpecEValue, out mMSGFSpecProb))
            {
                mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            }
        }
    }
}