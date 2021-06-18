
namespace PeptideListToXML
{
    public class clsPSMInfo
    {
        public const double MSGF_SPEC_NOT_DEFINED = 100.0;
        protected PHRPReader.Data.PSM mPSM;
        protected string mSpectrumKey;
        protected double mMSGFSpecProb;

        public PHRPReader.Data.PSM PSM
        {
            get
            {
                return mPSM;
            }
        }

        public string SpectrumKey
        {
            get
            {
                return mSpectrumKey;
            }
        }

        public double MSGFSpecProb
        {
            get
            {
                return mMSGFSpecProb;
            }
        }

        public clsPSMInfo(string spectrumKey, PHRPReader.Data.PSM psm)
        {
            mSpectrumKey = spectrumKey;
            mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            mPSM = psm;

            if (mPSM is null)
            {
                mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            }
            else if (!double.TryParse(mPSM.MSGFSpecEValue, out mMSGFSpecProb))
            {
                mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            }
        }
    }
}