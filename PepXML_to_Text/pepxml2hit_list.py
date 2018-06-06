#!/usr/bin/python

#
# Originally from http://code.google.com/p/massspec-toolbox/source/browse/trunk/search/sequest-pepxml2hit_list.py
# Modified by Matthew Monroe in September 2012 to extract additional columns
#

import os
import sys
import re
import pepxml

usage_mesg = 'Usage: pepxml2hit_list.py <.pepxml file>'

if( len(sys.argv) != 2 ):
    print usage_mesg
    sys.exit(1)

filename_pepxml = sys.argv[1]
if( not os.access(filename_pepxml,os.R_OK) ):
    print "%s is not accessible."%filename_pepxml
    print usage_mesg
    sys.exit(1)

print 'Reading %s'%(filename_pepxml)

PSM = pepxml.parse_by_filename(filename_pepxml)

filename_out = filename_pepxml
filename_out = re.sub('.pepxml$','',filename_out)
filename_out += '.txt'

print 'Creating %s'%(filename_pepxml)
sys.stderr.write("Write %s ... \n"%filename_out)
f_out = open(filename_out,'w')
f_out.write("Spectrum_ID\tCharge\tNeutralMass\tPeptide\tProtein\tMissedCleavages\tXcorr\tDeltaCn\tDeltaCn2\tRankXc\tXcRatio\tIons_Observed\tIons_Expected\tNumTrypticEnds\tMSGF_SpecProb\tScan_Scan\tEnd_Scan\tRetentionTime_Sec\n")
intLinesWritten = 0
for spectrum_id in PSM.keys():
    charge = PSM[spectrum_id]['charge']
    neutral_mass = PSM[spectrum_id]['neutral_mass']
    best_peptide = ''
    best_protein = ''
    best_xcorr = 0
    missed_cleavages = 0
    best_deltacnStar = 0
    best_RankXc = 0
    best_XcRatio = 0
    best_Ions_Observed = 0
    best_Ions_Expected = 0
    best_NumTrypticEnds = 0
    best_msgfspecprob = 1
    StoreHit = 0
    HitsParsed = 0
        
    for tmp_hit in PSM[spectrum_id]['search_hit']:
        msgfspecprob = tmp_hit.setdefault('msgfspecprob',1)
    
        StoreHit = 0
        if (HitsParsed == 0):
            StoreHit = 1
            best_msgfspecprob = msgfspecprob
            
            start_scan = tmp_hit.setdefault('start_scan',0)
            end_scan = tmp_hit.setdefault('end_scan',0)
            retention_time_sec = tmp_hit.setdefault('retention_time_sec',0)
                 
        elif (best_msgfspecprob < 1):
            if (msgfspecprob < best_msgfspecprob):
                StoreHit = 1
        elif (tmp_hit['xcorr'] > best_xcorr ):
            StoreHit = 1
        

        if( StoreHit == 1 ):
            best_xcorr = tmp_hit['xcorr']
            best_peptide = tmp_hit['peptide']
            best_protein = tmp_hit['protein']
            best_deltacn = tmp_hit['deltacn']
            missed_cleavages = tmp_hit['missed_cleavages']
            best_deltacnStar = tmp_hit.setdefault('deltacnstar',0)
            best_RankXc = tmp_hit.setdefault('RankXc',0)
            best_XcRatio = tmp_hit.setdefault('XcRatio',0)
            best_Ions_Observed = tmp_hit.setdefault('Ions_Observed',0)
            best_Ions_Expected = tmp_hit.setdefault('Ions_Expected',0)
            best_NumTrypticEnds = tmp_hit.setdefault('NumTrypticEnds',0)
            best_msgfspecprob = tmp_hit.setdefault('msgfspecprob',1)
       
        HitsParsed += 1

    if (HitsParsed > 0):
        f_out.write("%s\t%s\t%f\t%s\t%s\t%d\t%f\t%f"%(spectrum_id, charge, neutral_mass, best_peptide, best_protein, missed_cleavages, best_xcorr, best_deltacn))

        f_out.write("\t%f\t%s\t%f\t%s\t%s\t%s\t%s"%(best_deltacnStar, best_RankXc, best_XcRatio, best_Ions_Observed, best_Ions_Expected, best_NumTrypticEnds, best_msgfspecprob))

        f_out.write("\t%s\t%s\t%f\n"%(start_scan, end_scan, retention_time_sec))
    
        intLinesWritten += 1
    
        if (intLinesWritten % 1000 == 0):
            print '%i / %i'%(intLinesWritten,len(PSM.keys()))
        
f_out.close()

print "Done"
