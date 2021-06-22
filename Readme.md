# Peptide List To XML

The PeptideListToXML application reads a tab-delimited text file created 
by the [Peptide Hit Results Processor](https://github.com/PNNL-Comp-Mass-Spec/PHRP) (PHRP)
and creates a PepXML with the appropriate information. The 
various _SeqInfo files created by PHRP must be present in the same directory as
the text file. If the MASIC Scan Stats file is also present, elution time
information will be extracted and included in the PepXML file. You should ideally
also include the name of the parameter file used by the MS/MS search engine.

## Example Data

Example input and output files are in the Data directory:
* MaxQuant_Example has MaxQuant results
* MSGFPlus_Example has MS-GF+ results
* XTandem_Example has X!Tandem results

## Example Command line 

```
PeptideListToXML.exe QC_Shew_13_05b_HCD_500ng_24Mar14_Tiger_14-03-04_msgfplus_syn.txt /E:MSGFPlus_PartTryp_MetOx_20ppmParTol.txt

PeptideListToXML.exe QC_Mam_19_01_Run3_02Jun21_Cicero_WBEH-20-09-08_maxq_syn.txt /E:MaxQuant_Tryp_Stat_CysAlk_Dyn_MetOx_NTermAcet_20ppmParTol.xml /NoMSGF
```

## Command Line Syntax

PeptideListToXML is a console application, and must be run from the Windows command prompt.

```
PeptideListToXML.exe /I:PHRPResultsFile [/O:OutputDirectoryPath]
 [/E:SearchEngineParamFileName] [/F:FastaFilePath] [/P:ParameterFilePath]
 [/H:PSMsPerSpectrumToStore] [/X] [/TopHitOnly] [/MaxProteins:100]
 [/PepFilter:PeptideFilterFilePath] [/ChargeFilter:ChargeList]
 [/NoMods] [/NoMSGF] [/NoScanStats] [/Preview]
 [/S:[MaxLevel]] [/A:AlternateOutputDirectoryPath] [/R] [/L] [/Q]
```

The input file path can contain the wildcard character * and should point to a
tab-delimited text file created by PHRP (for example, Dataset_syn.txt,
Dataset_xt.txt, Dataset_msgfplus_syn.txt or Dataset_maxq_syn.txt). 

The output directory switch is optional. If omitted, the output file will be
created in the same directory as the input file.

Use `/E` to specify the name of the parameter file used by the MS/MS search engine
(must be in the same directory as the PHRP results file).
* For X!Tandem results, the default_input.xml and taxonomy.xml files must also be present in the input directory.

Use `/F` to specify the path to the FASTA file to store in the PepXML file
* Ignored if `/E` is provided and the search engine parameter file defines the FASTA file to 
search (this is the case for SEQUEST and X!Tandem but not Inspect or MS-GF+).

Use `/H` to specify the number of matches (aka hits) per spectrum to store
* The default is 3
* Use `/H:0` to keep all PSMs

Use `/X` to specify that peptides with X residues should be skipped

Use `/TopHitOnly` to specify that each scan should only include a single peptide
match (regardless of charge)

Use `/MaxProteins` to define the maximum number of proteins to track for each PSM
* The default is 100
Use `/PepFilter:File` to use a text file to filter the peptides included in the
output file (one peptide sequence per line)

Use `/ChargeFilter:ChargeList` to specify one or more charges to filter on for
inclusion in the output file. Examples:
* Only 2+ peptides: `/ChargeFilter:2`
* 2+ and 3+ peptides: `/ChargeFilter:2,3`

By default, the _ModSummary file and SeqInfo files are loaded and used to
determine the modified residues
* Use `/NoMods` to skip these files

By default, the _msgf.txt file is loaded to associated MSGF SpecProb values with the results
* Use `/NoMSGF` to skip this file

By default, the MASIC _ScanStats.txt and _ScanStatsEx.txt files are loaded to
determine elution times for scan numbers
* Use `/NoScanStats` to skip these files

Use `/Preview` to preview the files that would be required for the specified
dataset (taking into account the other command line switches used)

Use `/P` to specify a parameter file to use. Options in this file will override
options specified for `/E`, `/F`, `/H`, and `/X`

Use `/S` to process all valid files in the input directory and subdirectories.
Include a number after `/S` (like `/S:2`) to limit the level of subdirectories to examine.
* When using `/S`, you can redirect the output of the results using `/A`
* When using `/S`, you can use `/R` to re-create the input directory hierarchy in the
alternate output directory (if defined).

Use `/L` to log messages to a file.

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

Licensed under the Apache License, Version 2.0; you may not use this program except 
in compliance with the License. You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
