
namespace PeptideListToXML
{
    public class clsPSMInfo
    {
        public const double MSGF_SPEC_NOT_DEFINED = 100d;
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

        public clsPSMInfo(string strSpectrumKey, PHRPReader.Data.PSM objPSM)
        {
            mSpectrumKey = strSpectrumKey;
            mMSGFSpecProb = MSGF_SPEC_NOT_DEFINED;
            mPSM = objPSM;
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