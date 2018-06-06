Option Strict On

Public Class clsPSMInfo

    Public Const MSGF_SPEC_NOTDEFINED As Double = 100

    Protected mPSM As PHRPReader.clsPSM
    Protected mSpectrumKey As String
    Protected mMSGFSpecProb As Double

    Public ReadOnly Property PSM As PHRPReader.clsPSM
        Get
            Return mPSM
        End Get
    End Property

    Public ReadOnly Property SpectrumKey As String
        Get
            Return mSpectrumKey
        End Get
    End Property

    Public ReadOnly Property MSGFSpecProb As Double
        Get
            Return mMSGFSpecProb
        End Get
    End Property

    Public Sub New(strSpectrumKey As String, objPSM As PHRPReader.clsPSM)
        mSpectrumKey = strSpectrumKey
        mMSGFSpecProb = MSGF_SPEC_NOTDEFINED
        mPSM = objPSM

        If mPSM Is Nothing Then
            mMSGFSpecProb = MSGF_SPEC_NOTDEFINED
        Else
            If Not Double.TryParse(mPSM.MSGFSpecEValue, mMSGFSpecProb) Then
                mMSGFSpecProb = MSGF_SPEC_NOTDEFINED
            End If
        End If
    End Sub

End Class
