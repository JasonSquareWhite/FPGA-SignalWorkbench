using System;using System.IO;using System.Linq;using SignalWorkbench;
class BoundaryChecks {
 static int checks;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
 static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,"Expected parameter rejection");}
 static void Main(string[] args){try{Run(args[0]);}catch(Exception e){Console.Error.WriteLine(e.Message);Environment.ExitCode=1;}}
 static void Run(string path){
  foreach(double width in new[]{1e-6,1e-10,1e-300})Reject(()=>{double b;Filters.KaiserOrder(new FilterSettings{Transition=width},out b);});
  Reject(()=>{double b;Filters.KaiserOrder(new FilterSettings{Fs=double.PositiveInfinity},out b);});
  double beta;Check(Filters.KaiserOrder(new FilterSettings{Transition=2000,Attenuation=60},out beta)==88,"Kaiser nominal order");Check(Math.Abs(beta-5.65326)<1e-12,"Kaiser beta");
  Reject(()=>Filters.IirDesign(new FilterSettings{Structure="IIR",Order=4,F1=.001,TestSignal="Impulse"}));
  Reject(()=>Filters.IirDesign(new FilterSettings{Structure="IIR",Order=4,Response="Bandpass",F1=5000,F2=5000.01,TestSignal="Impulse"}));
  foreach(int order in new[]{512,1024,2048}){var r=Filters.Design(new FilterSettings{Order=order,TestSignal="Impulse"});Check(r.Impulse.Length==order+1,"Full FIR impulse length");Check(r.Impulse.SequenceEqual(r.Fir),"FIR impulse equals all taps");Check(r.ImpulseQ.SequenceEqual(r.FirQ),"Quantized FIR impulse equals all taps");}
  using(var w=new StreamWriter(path)){w.WriteLine("case,method,response,order,f1,f2,fs,section,b0,b1,b2,a0,a1,a2");int id=0;
   foreach(string method in new[]{"Butterworth","ChebyshevI","ChebyshevII"})foreach(int order in new[]{1,4,12})foreach(string response in new[]{"Lowpass","Highpass","Bandpass","Bandstop"}){
    double[] edges=response.StartsWith("Band")?new[]{.1,.5,.9}:new[]{.0001,.001,.1,.9999};foreach(double edge in edges){var s=new FilterSettings{Structure="IIR",Method=method,Order=order,Response=response,F1=edge*24000,F2=(edge+.000101)*24000,TestSignal="Impulse"};var sos=Filters.IirDesign(s);Check(Filters.PoleRadius(sos)<1,"Float stability at boundary");for(int i=0;i<sos.Length;i++)w.WriteLine(id+","+method+","+response+","+order+","+Engine.F(s.F1)+","+Engine.F(s.F2)+",48000,"+i+","+string.Join(",",sos[i].Select(Engine.F)));id++;}
   }Console.WriteLine("PASS: "+checks+" boundary assertions; "+id+" extreme IIR designs for independent reference");
  }
 }
}
