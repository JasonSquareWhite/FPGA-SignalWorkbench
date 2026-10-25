import sys,csv,json
from pathlib import Path
from collections import defaultdict
import numpy as np
from scipy import signal
cases=defaultdict(list)
for row in csv.DictReader(open(sys.argv[1],encoding='utf-8-sig')): cases[row['case']].append(row)
worst=0.;worst_case=None
for rows in cases.values():
 r=rows[0];fs=float(r['fs']);f1=float(r['f1']);f2=float(r['f2']);band=r['response'].startswith('Band');method=r['method'];order=int(r['order'])
 cut=[f1,f2] if band else f1;extra=[] if method=='Butterworth' else [1 if method=='ChebyshevI' else 60]
 ref={'Butterworth':signal.butter,'ChebyshevI':signal.cheby1,'ChebyshevII':signal.cheby2}[method](order,*extra,cut,btype=r['response'].lower(),fs=fs,output='sos')
 actual=np.array([[float(row[k]) for k in ['b0','b1','b2','a0','a1','a2']] for row in rows])
 f=np.unique(np.r_[np.linspace(0,fs/2,2049),np.geomspace(fs*1e-7,fs/2,2049),f1,np.linspace(max(0,f1-(f2-f1)),min(fs/2,f2+(f2-f1)),4097) if band else np.clip(f1*np.linspace(.1,2,1025),0,fs/2),fs/2-np.geomspace(fs*1e-7,fs/2,2049)])
 _,h=signal.freqz_sos(ref,worN=f,fs=fs);_,q=signal.freqz_sos(actual,worN=f,fs=fs);err=float(np.max(np.abs(h-q)))
 if err>worst:worst=err;worst_case={k:r[k] for k in ['case','method','response','order','f1','f2']}
 assert np.isfinite(err) and err<1e-6,(r['case'],err,r)
summary={'designs':len(cases),'max_complex_response_error':worst,'worst_case':worst_case,'tolerance':1e-6}
print(json.dumps(summary,indent=2));Path(sys.argv[1]+'.summary.json').write_text(json.dumps(summary,indent=2))
