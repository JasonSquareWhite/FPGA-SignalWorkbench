from pathlib import Path
import xml.etree.ElementTree as ET
import csv,sys,json
import numpy as np
from scipy import signal
root=Path(sys.argv[1]);worst_coeff=worst_response=worst_impulse=0.;count=0
names={'Rectangular':'boxcar','Hann':'hann','Hamming':'hamming','Blackman':'blackman','BlackmanHarris':'blackmanharris','Nuttall':'nuttall','Bartlett':'bartlett','BartlettHann':'barthann','FlatTop':'flattop'}
for path in root.glob('*.xml'):
 if not path.stem.isdigit(): continue
 s={e.tag:e.text for e in ET.parse(path).getroot()};order=int(s['Order']);fs=float(s['Fs']);response=s['Response'];band=response.startswith('Band');cut=[float(s['F1']),float(s['F2'])] if band else float(s['F1']);rows=list(csv.DictReader(path.with_suffix('.csv').open(encoding='utf-8-sig')))
 coeff=np.array([float(r['float_coefficient']) for r in rows if r['name']!='a0']);w=np.linspace(0,np.pi,257);imp=np.zeros(512);imp[0]=1
 if s['Structure']=='FIR':
  name=s['Window'];window=names.get(name,(name.lower(),float(s['WindowParameter'])))
  b=signal.firwin(order+1,cut,window=window,pass_zero=response.lower(),fs=fs,scale=s['Scale']=='true');delta=np.max(np.abs(coeff-b));worst_coeff=max(worst_coeff,delta);assert delta<1e-11,(path,delta)
  _,h=signal.freqz(b,worN=w);y=signal.lfilter(b,[1],imp)
 else:
  method={'Butterworth':signal.butter,'ChebyshevI':signal.cheby1,'ChebyshevII':signal.cheby2}[s['Method']];extra=[] if s['Method']=='Butterworth' else [float(s['Ripple'] if s['Method']=='ChebyshevI' else s['Attenuation'])]
  sos=method(order,*extra,cut,btype=response.lower(),fs=fs,output='sos');_,h=signal.freqz_sos(sos,worN=w);y=signal.sosfilt(sos,imp)
 actual=np.loadtxt(path.with_suffix('.response.csv'),delimiter=',');actual=actual[:,0]+1j*actual[:,1];delta=np.max(np.abs(h-actual));worst_response=max(worst_response,delta);assert delta<1e-8,(path,'response',delta)
 actual=np.loadtxt(path.with_suffix('.impulse.csv'));y=y[:len(actual)];delta=np.max(np.abs(y-actual));worst_impulse=max(worst_impulse,delta);assert delta<1e-8,(path,'impulse',delta);count+=1
summary=dict(designs=count,max_fir_coefficient_error=worst_coeff,max_complex_response_error=worst_response,max_impulse_error=worst_impulse)
print(json.dumps(summary,indent=2));(root/'reference_summary.json').write_text(json.dumps(summary,indent=2))
