Option Strict On

' This program reads a tab-delimited text file of peptide sequence and
' creates a PepXML or mzIdentML file with the appropriate information

' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started April 13, 2012

' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://omics.pnl.gov
' -------------------------------------------------------------------------------
' 
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0

Module modMain

	Public Const PROGRAM_DATE As String = "April 23, 2012"

    Private mInputFilePath As String
    Private mOutputFolderPath As String             ' Optional
    Private mParameterFilePath As String            ' Optional

	Private mFastaFilePath As String
	Private mSearchEngineParamFileName As String

	' Future enum; mzIdentML is not yet supported
	' Private mOutputFormat As clsPeptideListToXML.ePeptideListOutputFormat

    Private mOutputFolderAlternatePath As String                ' Optional
    Private mRecreateFolderHierarchyInAlternatePath As Boolean  ' Optional

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer

    Private mLogMessagesToFile As Boolean
    Private mLogFilePath As String = String.Empty
    Private mLogFolderPath As String = String.Empty

    Private mQuietMode As Boolean

    Private WithEvents mPeptideListConverter As clsPeptideListToXML
    Private mLastProgressReportTime As System.DateTime
    Private mLastProgressReportValue As Integer

    Public Function Main() As Integer
        ' Returns 0 if no error, error code if an error

        Dim intReturnCode As Integer
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        ' Initialize the options
        intReturnCode = 0
        mInputFilePath = String.Empty
        mOutputFolderPath = String.Empty
        mParameterFilePath = String.Empty

		mFastaFilePath = String.Empty
		mSearchEngineParamFileName = String.Empty

		' Future enum; mzIdentML is not yet supported
		' mOutputFormat = clsPeptideListToXML.ePeptideListOutputFormat.PepXML

        mRecurseFolders = False
        mRecurseFoldersMaxLevels = 0

        mQuietMode = False
        mLogMessagesToFile = False
        mLogFilePath = String.Empty
        mLogFolderPath = String.Empty

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If Not blnProceed OrElse _
               objParseCommandLine.NeedToShowHelp OrElse _
               objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse _
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else

                mPeptideListConverter = New clsPeptideListToXML

                ' Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
                With mPeptideListConverter
                    .ShowMessages = Not mQuietMode
                    .LogMessagesToFile = mLogMessagesToFile

					.FastaFilePath = mFastaFilePath
					.SearchEngineParamFileName = mSearchEngineParamFileName

					' .OutputFormat = mOutputFormat

                End With

                If mRecurseFolders Then
                    If mPeptideListConverter.ProcessFilesAndRecurseFolders(mInputFilePath, mOutputFolderPath, mOutputFolderAlternatePath, mRecreateFolderHierarchyInAlternatePath, mParameterFilePath, mRecurseFoldersMaxLevels) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mPeptideListConverter.ErrorCode
                    End If
                Else
                    If mPeptideListConverter.ProcessFilesWildcard(mInputFilePath, mOutputFolderPath, mParameterFilePath) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mPeptideListConverter.ErrorCode
                        If intReturnCode <> 0 AndAlso Not mQuietMode Then
                            ShowErrorMessage("Error while processing: " & mPeptideListConverter.GetErrorMessage())
                        End If
                    End If
                End If

                DisplayProgressPercent(mLastProgressReportValue, True)
            End If

        Catch ex As Exception
            ShowErrorMessage("Error occurred in modMain->Main: " & System.Environment.NewLine & ex.Message)
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Sub DisplayProgressPercent(ByVal intPercentComplete As Integer, ByVal blnAddCarriageReturn As Boolean)
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
        If intPercentComplete > 100 Then intPercentComplete = 100
        Console.Write("Processing: " & intPercentComplete.ToString() & "% ")
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
    End Sub

    Private Function GetAppVersion() As String
        Return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
		Dim strValidParameters() As String = New String() {"I", "O", "M", "P", "F", "E", "S", "A", "R", "L", "Q"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
                Return False
            Else
                With objParseCommandLine
                    ' Query objParseCommandLine to see if various parameters are present
                    If .RetrieveValueForParameter("I", strValue) Then
                        mInputFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mInputFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("O", strValue) Then mOutputFolderPath = strValue

					' Future enum; mzIdentML is not yet supported
					'If .RetrieveValueForParameter("M", strValue) Then
					'    mOutputFormat = clsPeptideListToXML.ePeptideListOutputFormat.mzIdentML
					'End If

                    If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

					If .RetrieveValueForParameter("F", strValue) Then mFastaFilePath = strValue
					If .RetrieveValueForParameter("E", strValue) Then mSearchEngineParamFileName = strValue



                    If .RetrieveValueForParameter("S", strValue) Then
                        mRecurseFolders = True
                        If Not Integer.TryParse(strValue, mRecurseFoldersMaxLevels) Then
                            mRecurseFoldersMaxLevels = 0
                        End If
                    End If

                    If .RetrieveValueForParameter("A", strValue) Then mOutputFolderAlternatePath = strValue
                    If .RetrieveValueForParameter("R", strValue) Then mRecreateFolderHierarchyInAlternatePath = True

                    If .RetrieveValueForParameter("L", strValue) Then mLogMessagesToFile = True
                    If .RetrieveValueForParameter("Q", strValue) Then mQuietMode = True

                End With

                Return True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error parsing the command line parameters: " & System.Environment.NewLine & ex.Message)
        End Try

        Return False

    End Function

    Private Sub ShowErrorMessage(ByVal strMessage As String)
        Dim strSeparator As String = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

    End Sub

    Private Sub ShowProgramHelp()

        Try
            Console.WriteLine("This program reads a tab-delimited text file of peptide sequence and " & _
                              "creates a PepXML or mzIdentML file with the appropriate information.")
            Console.WriteLine()

            Console.WriteLine("Program syntax:")
            Console.WriteLine(System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) & _
                              " /I:SourceFastaOrTextFile [/O:OutputFolderPath] [/M]")
			Console.WriteLine(" [/P:ParameterFilePath] [/F:FastaFilePath] [/E:SearchEngineParamFileName]")
            Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L] [/Q]")
            Console.WriteLine()
            Console.WriteLine("The input file path can contain the wildcard character * and should point to a tab-delimited text file. " & _
                              "The output folder switch is optional.  If omitted, the output file will be created in the same folder as the input file. " & _
                              "By default, the output file will be a PepXML file; use /M to instead create a mzIdentML file. ")
            Console.WriteLine()

            Console.WriteLine("Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. " & _
                              "When using /S, you can redirect the output of the results using /A. " & _
                              "When using /S, you can use /R to re-create the input folder hierarchy in the alternate output folder (if defined).")

            Console.WriteLine("Use /L to log messages to a file.  If /Q is used, then no messages will be displayed at the console.")
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://ncrr.pnl.gov/ or http://omics.pnl.gov")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

        Catch ex As Exception
			ShowErrorMessage("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

    Private Sub mMotifExtractor_ProgressChanged(ByVal taskDescription As String, ByVal percentComplete As Single) Handles mPeptideListConverter.ProgressChanged
        Const PERCENT_REPORT_INTERVAL As Integer = 25
        Const PROGRESS_DOT_INTERVAL_MSEC As Integer = 250

        If percentComplete >= mLastProgressReportValue Then
            If mLastProgressReportValue > 0 Then
                Console.WriteLine()
            End If
            DisplayProgressPercent(mLastProgressReportValue, False)
            mLastProgressReportValue += PERCENT_REPORT_INTERVAL
            mLastProgressReportTime = DateTime.UtcNow
        Else
            If DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC Then
                mLastProgressReportTime = DateTime.UtcNow
                Console.Write(".")
            End If
        End If
    End Sub

    Private Sub mMotifExtractor_ProgressReset() Handles mPeptideListConverter.ProgressReset
        mLastProgressReportTime = DateTime.UtcNow
        mLastProgressReportValue = 0
    End Sub
End Module
