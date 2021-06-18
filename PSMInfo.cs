﻿
namespace PeptideListToXML
{
    public class PSMInfo
    {
        public const double MSGF_SPEC_NOT_DEFINED = 100.0;
        private readonly PHRPReader.Data.PSM mPSM;
        private readonly string mSpectrumKey;
        private readonly double mMSGFSpecProb;

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

        public PSMInfo(string spectrumKey, PHRPReader.Data.PSM psm)
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