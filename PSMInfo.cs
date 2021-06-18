
namespace PeptideListToXML
{
    public class PSMInfo
    {
        public const double MSGF_SPEC_NOT_DEFINED = 100.0;

        private readonly double mMSGFSpecProb;

        public PHRPReader.Data.PSM PSM { get; }

        public string SpectrumKey { get; }

        public double MSGFSpecProb => mMSGFSpecProb;

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