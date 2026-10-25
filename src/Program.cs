using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Globalization;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace SignalWorkbench {
static class Program {
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length>0&&args[0]=="--self-test"){Checks.Run(args[1]);return 0;}
   if(args.Length>0&&args[0]=="--reference"){Checks.Reference(args[1]);Checks.WaveReference(args[1]+".waves.csv");return 0;}
   if(args.Length>0&&args[0]=="--render"){using(var form=new MainForm())form.Render(args[1]);return 0;}
   if(args.Length>0&&args[0]=="--ui-test"){using(var form=new MainForm()){form.Show();Application.DoEvents();File.WriteAllText(args[1],form.UiTest());}return 0;}
   Application.Run(new MainForm());return 0;
  }catch(Exception ex){if(args.Length>1){File.WriteAllText(args[1]+".error.txt",ex.ToString());}else MessageBox.Show(ex.Message,"启动失败");return 1;}
 }
}
static class Checks {
 static int passed;static void Assert(bool yes,string name){if(!yes)throw new Exception("FAIL: "+name);passed++;}
 static void Near(double a,double b,string name){Assert(Math.Abs(a-b)<1e-11,name+" actual="+Engine.F(a)+" expected="+Engine.F(b));}
 static void Throws(Action a,string name){bool thrown=false;try{a();}catch(ArgumentException){thrown=true;}Assert(thrown,name);}
 public static void Run(string dir){Directory.CreateDirectory(dir);passed=0;
  double[] h=Engine.Window("Hann",5,false,0);for(int i=0;i<5;i++)Near(h[i],new[]{0,.5,1,.5,0}[i],"Hann N5");
  h=Engine.Window("Hann",4,true,0);for(int i=0;i<4;i++)Near(h[i],new[]{0,.5,1,.5}[i],"periodic Hann N4");
  foreach(string kind in Engine.Windows)foreach(int n in new[]{2,3,16,17,1024}){
   double p=kind=="Tukey"?.5:kind=="Gaussian"?5:8.6;var w=Engine.Window(kind,n,false,p);for(int i=0;i<n;i++)Near(w[i],w[n-1-i],"window symmetry "+kind);
   var wp=Engine.Window(kind,n,true,p);var we=Engine.Window(kind,n+1,false,p);for(int i=0;i<n;i++)Near(wp[i],we[i],"periodic extension "+kind);
  }
  var s=new Settings{N=4,Depth=4,Width=8,Fraction=7};var r=Engine.Quantize(s,new[]{-1.0,-.5,0,.5});Assert(r.Words.SequenceEqual(new ulong[]{128,192,0,64}),"signed 8-bit golden");
  Throws(()=>Engine.Quantize(s,new[]{1.0,0,0,0}),"+1 Q7 overflow");s.Overflow="Saturate";r=Engine.Quantize(s,new[]{1.0,-2,0,0});Assert(r.Saturated==2&&r.Words[0]==127&&r.Words[1]==128,"saturation golden");
  s.Fraction=0;s.Overflow="Error";r=Engine.Quantize(s,new[]{.5,1.5,-.5,-1.5});Assert(r.Integers.SequenceEqual(new long[]{0,2,0,-2}),"ties to even");
  s.Rounding="Truncate";r=Engine.Quantize(s,new[]{1.9,-1.9,0,0});Assert(r.Integers[0]==1&&r.Integers[1]==-1,"truncate");s.Rounding="Floor";r=Engine.Quantize(s,new[]{1.9,-1.9,0,0});Assert(r.Integers[0]==1&&r.Integers[1]==-2,"floor");
  s=new Settings{N=4,Depth=6,Width=8,Fraction=0,Encoding="Offset"};r=Engine.Quantize(s,new[]{-128.0,-1,0,127});Assert(r.Words.SequenceEqual(new ulong[]{0,127,128,255,128,128}),"offset and zero padding");
  s=new Settings{N=2,Depth=2,Width=32,Fraction=0,Encoding="Unsigned"};r=Engine.Quantize(s,new[]{0.0,4294967295.0});Assert(r.Words[1]==4294967295UL,"uint32 max");
  s.Encoding="Signed";r=Engine.Quantize(s,new[]{-2147483648.0,2147483647.0});Assert(r.Words.SequenceEqual(new ulong[]{2147483648UL,2147483647UL}),"int32 limits");
  s=new Settings{N=4,Depth=4,Mode=1,Width=16,Fraction=14,Wave="Sine",Cycles=1,Minimum=-1,Maximum=1};var x=Engine.GenerateValues(s);for(int i=0;i<4;i++)Near(x[i],new[]{0.0,1,0,-1}[i],"sine golden");
  s.Wave="Triangle";x=Engine.GenerateValues(s);for(int i=0;i<4;i++)Near(x[i],new[]{-1.0,0,1,0}[i],"triangle golden");s.Wave="Square";x=Engine.GenerateValues(s);Assert(x.SequenceEqual(new[]{1.0,1,-1,-1}),"square golden");
  s.Wave="SawUp";x=Engine.GenerateValues(s);Assert(x.SequenceEqual(new[]{-1.0,-.5,0,.5}),"saw endpoint exclusion");s.FrequencyMode=true;s.Frequency=s.SampleRate/4;var xf=Engine.GenerateValues(s);Assert(x.SequenceEqual(xf),"frequency cycles equivalence");
  s.Frequency=s.SampleRate/2;Throws(()=>Engine.Generate(s),"Nyquist guard");s.N=1;Throws(()=>Engine.Generate(s),"length guard");
  Throws(()=>Engine.Normalize(new[]{0.0,0},"Peak"),"zero normalization guard");Throws(()=>Engine.Window("Kaiser",16,false,51),"beta guard");Throws(()=>Engine.Window("Gaussian",16,false,0),"sigma guard");Throws(()=>Engine.Window("Tukey",16,false,2),"alpha guard");
  s=new Settings();foreach(string platform in new[]{"Vivado","Altera"})foreach(int radix in new[]{2,10,16})foreach(int width in new[]{1,8,16,32}){s.Platform=platform;s.Radix=radix;s.Width=width;s.Fraction=width-1;s.Gain=width==1?0:1-1/Math.Pow(2,width-1);r=Engine.Generate(s);string file=Path.Combine(dir,platform+"_W"+width+"_R"+radix+(platform=="Vivado"?".coe":".mif"));Engine.Export(file,r);Engine.VerifyFile(file,r);passed++;}
  s=new Settings();s.Save(Path.Combine(dir,"roundtrip.xml"));var loaded=Settings.Load(Path.Combine(dir,"roundtrip.xml"));Assert(Engine.Generate(s).Words.SequenceEqual(Engine.Generate(loaded).Words),"settings roundtrip");
  int fft;var db=Engine.Spectrum(Enumerable.Repeat(1.0,4096).ToArray(),out fft);Near(db[0],0,"DC FFT peak");Assert(db.Skip(1).All(v=>v<=-159.9),"DC FFT other bins");var tone=Enumerable.Range(0,4096).Select(i=>Math.Sin(2*Math.PI*16*i/4096)).ToArray();db=Engine.Spectrum(tone,out fft);Near(db[16],0,"sine FFT bin");
  s=new Settings{N=1048576,Depth=1048576};var sw=System.Diagnostics.Stopwatch.StartNew();r=Engine.Generate(s);sw.Stop();Assert(r.Words.Length==1048576,"large point count");
  File.WriteAllText(Path.Combine(dir,"self-test.txt"),"PASS: "+passed+" assertions\r\nMaximum N generation: "+sw.ElapsedMilliseconds+" ms\r\nCOE/MIF roundtrip: 2 platforms x 3 radices x 4 widths\r\nNo hardware programming performed.\r\n");
  Examples(dir);
 }
 public static void Examples(string dir){var s=new Settings();var r=Engine.Generate(s);Engine.Export(Path.Combine(dir,"hann_1024_w16_f15.coe"),r);Engine.ExportCsv(Path.Combine(dir,"hann_1024_w16_f15.csv"),r);s.Save(Path.Combine(dir,"hann_1024_w16_f15.xml"));s.Mode=1;r=Engine.Generate(s);Engine.Export(Path.Combine(dir,"sine_1024_w16_f15.coe"),r);Engine.ExportCsv(Path.Combine(dir,"sine_1024_w16_f15.csv"),r);s.Save(Path.Combine(dir,"sine_1024_w16_f15.xml"));}
 public static void Reference(string path){using(var w=new StreamWriter(path,false,Encoding.ASCII)){w.WriteLine("kind,n,periodic,parameter,index,value");foreach(string kind in Engine.Windows)foreach(int n in new[]{2,3,4,5,16,17,64,65,1024})foreach(bool periodic in new[]{false,true})foreach(double p in kind=="Kaiser"?new[]{0.0,8.6,50}:kind=="Tukey"?new[]{0.0,.25,1}:kind=="Gaussian"?new[]{.25,5,128}:new[]{8.6}){var a=Engine.Window(kind,n,periodic,p);for(int i=0;i<n;i++)w.WriteLine(kind+","+n+","+periodic+","+Engine.F(p)+","+i+","+Engine.F(a[i]));}}}
 public static void WaveReference(string path){using(var w=new StreamWriter(path,false,Encoding.ASCII)){w.WriteLine("wave,n,cycles,phase,duty,index,value");foreach(string wave in Engine.Waves)foreach(int n in new[]{16,65,1024})foreach(double cycles in new[]{1.0,3.25})foreach(double phase in new[]{0.0,37.0,-90.0})foreach(double duty in new[]{.25,.5,.7}){var s=new Settings{Mode=1,Wave=wave,N=n,Depth=n,Cycles=cycles,Phase=phase,Duty=duty,Minimum=-.8,Maximum=.8};var a=Engine.GenerateValues(s);for(int i=0;i<n;i++)w.WriteLine(wave+","+n+","+Engine.F(cycles)+","+Engine.F(phase)+","+Engine.F(duty)+","+i+","+Engine.F(a[i]));}}}
}
}
