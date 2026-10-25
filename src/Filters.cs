using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SignalWorkbench {
public class FilterSettings {
 public string Structure="FIR",Response="Lowpass",Method="Butterworth",Window="Hamming",Rounding="Even",Overflow="Error",Format="FIR COE",TestSignal="TwoTone";
 public int Order=64,Width=18,Fraction=16,SignalLength=1024,InputColumn=1;
 public double Fs=48000,F1=5000,F2=10000,Ripple=1,Attenuation=60,WindowParameter=8.6,Transition=2000,Tone1=1000,Tone2=12000;
 public bool Scale=true;
 public FilterSettings Copy(){return (FilterSettings)MemberwiseClone();}
 public void Save(string path){using(var w=new StreamWriter(path,false,new UTF8Encoding(false)))new XmlSerializer(typeof(FilterSettings)).Serialize(w,this);}
 public static FilterSettings Load(string path){using(var r=System.Xml.XmlReader.Create(path,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit}))return (FilterSettings)new XmlSerializer(typeof(FilterSettings)).Deserialize(r);}
}
public class FilterResult {
 public FilterSettings Config;public double[] Fir,FirQ;public double[][] Sos,SosQ;public Result Codes;
 public double[] Magnitude,MagnitudeQ,Phase,PhaseQ,Impulse,ImpulseQ,Input,Output,OutputQ;
 public double MaxPole,MaxPoleQ,MaxResponseError;public int ActualOrder;public bool Stable,StableQ;public string Report;
}
public static class Filters {
 public const int Grid=4097;
 public static void Require(bool v,string why){if(!v)throw new ArgumentException(why);}
 public static void Validate(FilterSettings s){
  Require(s.Structure=="FIR"||s.Structure=="IIR","未知滤波器结构。");Require(new[]{"FIR COE","ROM COE","Altera MIF"}.Contains(s.Format),"未知导出格式。");Require(new[]{"Lowpass","Highpass","Bandpass","Bandstop"}.Contains(s.Response),"未知响应类型。");
  foreach(double d in new[]{s.Fs,s.F1,s.F2,s.Ripple,s.Attenuation,s.WindowParameter,s.Transition,s.Tone1,s.Tone2})Require(Engine.Finite(d),"滤波参数必须为有限数值。");
  Require(s.Fs>0&&s.Fs<=1e12,"采样率必须在 (0,1e12] Hz。");Require(s.F1>0&&s.F1<s.Fs/2,"第一边缘频率必须在 (0,Fs/2) 内。");
  if(IsBand(s))Require(s.F2>s.F1&&s.F2<s.Fs/2,"第二边缘频率必须满足 F1 < F2 < Fs/2。");
  Require(s.Width>=2&&s.Width<=32&&s.Fraction>=0&&s.Fraction<s.Width,"系数位宽 2～32 bit，小数位须在 0～W-1。");
  Require(new[]{"Even","Truncate","Floor"}.Contains(s.Rounding),"未知舍入方式。");Require(s.Overflow=="Error"||s.Overflow=="Saturate","未知溢出策略。");
  Require(s.SignalLength>=16&&s.SignalLength<=65536,"信号点数须为16～65536。");Require(s.InputColumn>=1&&s.InputColumn<=256,"数据列号须为1～256。");
  Require(new[]{"TwoTone","Impulse","Step","Imported"}.Contains(s.TestSignal),"未知测试信号。");
  if(s.TestSignal=="TwoTone")Require(s.Tone1>=0&&s.Tone1<s.Fs/2&&s.Tone2>=0&&s.Tone2<s.Fs/2,"测试正弦频率必须在 [0,Fs/2) 内。");
  if(s.Structure=="FIR"){
   Require(s.Order>=1&&s.Order<=2048,"FIR阶数须为1～2048（抽头数=阶数+1）。");Require(Engine.Windows.Contains(s.Window),"未知窗函数。");
   if(s.Response=="Highpass"||s.Response=="Bandstop")Require(s.Order%2==0,"高通/带阻的通带包含奈奎斯特频率，需要奇数抽头（偶数阶），以避免Type II FIR在该频率强制为零。");
  }else{
   double nyquist=s.Fs/2,margin=nyquist*1e-4;
   Require(s.F1>=margin&&s.F1<=nyquist-margin&&(!IsBand(s)||(s.F2<=nyquist-margin&&s.F2-s.F1>=margin)),"当前双精度 IIR 设计要求边缘距 0/Fs/2、带宽均不小于 (Fs/2)×0.0001。请调整采样率或频率；不输出精度不足的系数。");
   Require(new[]{"Butterworth","ChebyshevI","ChebyshevII"}.Contains(s.Method),"未知IIR方法。");Require(s.Order>=1&&s.Order<=12,"IIR原型阶数支持1～12；带通/带阻最终阶数为其2倍。");
   Require(s.Ripple>0&&s.Ripple<=30,"Chebyshev I通带纹波须在(0,30] dB。");Require(s.Attenuation>0&&s.Attenuation<=180,"Chebyshev II阻带衰减须在(0,180] dB。");
  }
 }
 public static bool IsBand(FilterSettings s){return s.Response=="Bandpass"||s.Response=="Bandstop";}
 static double Sinc(double x){return Math.Abs(x)<1e-14?1:Math.Sin(Math.PI*x)/(Math.PI*x);}
 public static double[] FirDesign(FilterSettings s){
  Validate(s);int n=s.Order+1;var w=Engine.Window(s.Window,n,false,s.WindowParameter);var h=new double[n];double a=s.F1/(s.Fs/2),b=s.F2/(s.Fs/2);
  for(int i=0;i<n;i++){
   double t=i-s.Order/2.0,lp=a*Sinc(a*t),lp2=b*Sinc(b*t);double v;
   if(s.Response=="Lowpass")v=lp;else if(s.Response=="Highpass")v=Sinc(t)-lp;else if(s.Response=="Bandpass")v=lp2-lp;else v=Sinc(t)-lp2+lp;
   h[i]=v*w[i];
  }
  if(s.Scale){double f=s.Response=="Highpass"?1:s.Response=="Bandpass"?(a+b)/2:0;double sum=0;for(int i=0;i<n;i++)sum+=h[i]*Math.Cos(Math.PI*f*(i-s.Order/2.0));Require(Math.Abs(sum)>1e-14,"通带归一化分母过小，请增加阶数或调整频率。");for(int i=0;i<n;i++)h[i]/=sum;}
  return h;
 }
 public static int KaiserOrder(FilterSettings s,out double beta){
  Require(Engine.Finite(s.Fs)&&s.Fs>0&&s.Fs<=1e12,"采样率必须在 (0,1e12] Hz。");
  Require(Engine.Finite(s.Transition)&&s.Transition>0&&s.Transition<s.Fs/2,"过渡带宽必须在(0,Fs/2)内。");Require(s.Attenuation>=8&&s.Attenuation<=180,"Kaiser估算衰减范围为8～180 dB。");
  double a=s.Attenuation;beta=a>50?0.1102*(a-8.7):a>21?0.5842*Math.Pow(a-21,.4)+.07886*(a-21):0;
  double estimate=Math.Ceiling((a-7.95)/(2.285*Math.PI*(s.Transition/(s.Fs/2))));Require(Engine.Finite(estimate)&&estimate<=2048,"估算阶数超过2048，请放宽过渡带或降低衰减要求。");int order=(int)estimate;order=Math.Max(2,order);if((s.Response=="Highpass"||s.Response=="Bandstop")&&order%2!=0)order++;
  Require(order<=2048,"估算阶数超过2048，请放宽过渡带或降低衰减要求。");return order;
 }
 static double Asinh(double x){return Math.Log(x+Math.Sqrt(x*x+1));}
 static List<Complex> Transform(List<Complex> r,string kind,double w1,double w2){
  var list=new List<Complex>();double bw=w2-w1,wo=Math.Sqrt(w1*w2);
  foreach(Complex p in r){
   if(kind=="Lowpass")list.Add(p*w1);else if(kind=="Highpass")list.Add(w1/p);
   else {Complex v=kind=="Bandpass"?p*bw/2:bw/(2*p);Complex q=Complex.Sqrt(v*v-wo*wo);list.Add(v+q);list.Add(v-q);}
  }return list;
 }
 static List<Complex[]> Pairs(List<Complex> roots){
  var remaining=new List<Complex>(roots);var pairs=new List<Complex[]>();
  while(remaining.Count>0){Complex a=remaining[0];remaining.RemoveAt(0);if(Math.Abs(a.Imaginary)<1e-8)a=new Complex(a.Real,0);
   if(remaining.Count==0){Require(Math.Abs(a.Imaginary)<1e-8,"无法配对复根。");pairs.Add(new[]{a});break;}
   int index=-1;double best=double.PositiveInfinity;
   for(int i=0;i<remaining.Count;i++){if(a.Imaginary==0&&Math.Abs(remaining[i].Imaginary)>1e-8)continue;double d=Complex.Abs(remaining[i]-Complex.Conjugate(a));if(d<best){best=d;index=i;}}
   if(index<0){Require(a.Imaginary==0,"找不到共轭根。");pairs.Add(new[]{a});continue;}
   Complex b=remaining[index];remaining.RemoveAt(index);if(a.Imaginary!=0)Require(Complex.Abs(b-Complex.Conjugate(a))<1e-6,"共轭根数值不一致。");else b=new Complex(b.Real,0);pairs.Add(new[]{a,b});
  }return pairs.OrderBy(p=>p.Max(z=>z.Magnitude)).ToList();
 }
 static double[] Polynomial(Complex[] pair){if(pair.Length==1)return new[]{1.0,-pair[0].Real,0.0};return new[]{1.0,-(pair[0]+pair[1]).Real,(pair[0]*pair[1]).Real};}
 public static double[][] IirDesign(FilterSettings s){
  Validate(s);var poles=new List<Complex>();var zeros=new List<Complex>();int n=s.Order;double mu=0;
  if(s.Method=="ChebyshevI")mu=Asinh(1/Math.Sqrt(Math.Pow(10,s.Ripple/10)-1))/n;
  if(s.Method=="ChebyshevII")mu=Asinh(Math.Sqrt(Math.Pow(10,s.Attenuation/10)-1))/n;
  for(int k=0;k<n;k++){double t=(2*k+1)*Math.PI/(2*n);Complex p=s.Method=="Butterworth"?new Complex(-Math.Sin(t),Math.Cos(t)):new Complex(-Math.Sinh(mu)*Math.Sin(t),Math.Cosh(mu)*Math.Cos(t));if(s.Method=="ChebyshevII"){p=1/p;if(Math.Abs(Math.Cos(t))>1e-12)zeros.Add(new Complex(0,1/Math.Cos(t)));}poles.Add(p);}
  int degree=poles.Count-zeros.Count;double w1=2*Math.Tan(Math.PI*s.F1/s.Fs),w2=2*Math.Tan(Math.PI*s.F2/s.Fs);
  poles=Transform(poles,s.Response,w1,w2);zeros=Transform(zeros,s.Response,w1,w2);
  if(s.Response=="Highpass"||s.Response=="Bandpass")for(int i=0;i<degree;i++)zeros.Add(Complex.Zero);
  if(s.Response=="Bandstop")for(int i=0;i<degree;i++){zeros.Add(new Complex(0,Math.Sqrt(w1*w2)));zeros.Add(new Complex(0,-Math.Sqrt(w1*w2)));}
  poles=poles.Select(p=>(2+p)/(2-p)).ToList();zeros=zeros.Select(z=>(2+z)/(2-z)).ToList();while(zeros.Count<poles.Count)zeros.Add(-Complex.One);
  var pp=Pairs(poles);var zz=Pairs(zeros);Require(pp.Count==zz.Count,"SOS配对数量错误。");var sections=new double[pp.Count][];
  // Match first-order sections first, then closest zero/pole angle; spread gain across sections.
  for(int i=0;i<pp.Count;i++){int j=0;double best=double.PositiveInfinity;for(int z=0;z<zz.Count;z++){double score=Math.Abs(zz[z].Average(r=>Math.Abs(r.Phase))-pp[i].Average(r=>Math.Abs(r.Phase)))+(zz[z].Length==pp[i].Length?0:100);if(score<best){best=score;j=z;}}var b=Polynomial(zz[j]);zz.RemoveAt(j);var a=Polynomial(pp[i]);sections[i]=new[]{b[0],b[1],b[2],1.0,a[1],a[2]};}
  double refOmega=s.Response=="Highpass"?Math.PI:s.Response=="Bandpass"?2*Math.Atan(Math.Sqrt(w1*w2)/2):0;
  double target=s.Method=="ChebyshevI"&&n%2==0?Math.Pow(10,-s.Ripple/20):1;
  double mag=Response(null,sections,refOmega).Magnitude;Require(Engine.Finite(mag)&&mag>1e-200,"IIR增益计算失败，请调整参数。");double gain=Math.Pow(target/mag,1.0/sections.Length);
  foreach(var sec in sections)for(int i=0;i<3;i++)sec[i]*=gain;
  // Balance section peaks to prevent large intermediate states in narrow-band cascades.
  double total=1;foreach(var sec in sections){double peak=0;for(int k=0;k<=4096;k++)peak=Math.Max(peak,Response(null,new[]{sec},Math.PI*k/4096).Magnitude);Require(peak>0&&Engine.Finite(peak),"SOS增益平衡失败。");for(int k=0;k<3;k++)sec[k]/=peak;total*=peak;}
  // Distribute residual gain equally after peak balancing.
  double residual=Math.Pow(total,1.0/sections.Length);foreach(var sec in sections)for(int k=0;k<3;k++)sec[k]*=residual;return sections;
 }
 public static Complex Response(double[] fir,double[][] sos,double omega){Complex z=Complex.FromPolarCoordinates(1,-omega);if(fir!=null){Complex h=0;for(int i=fir.Length-1;i>=0;i--)h=h*z+fir[i];return h;}Complex result=1;foreach(var q in sos){result*=(q[0]+q[1]*z+q[2]*z*z)/(q[3]+q[4]*z+q[5]*z*z);}return result;}
 public static double PoleRadius(double[][] sos){if(sos==null)return 0;double max=0;foreach(var a in sos){Complex disc=Complex.Sqrt(new Complex(a[4]*a[4]-4*a[3]*a[5],0));max=Math.Max(max,Math.Max(((-a[4]+disc)/(2*a[3])).Magnitude,((-a[4]-disc)/(2*a[3])).Magnitude));}return max;}
 public static double[] Apply(double[] input,double[] fir,double[][] sos){
  var y=new double[input.Length];if(fir!=null){Require((long)input.Length*fir.Length<=140000000,"信号长度×抽头数过大，请减少信号长度或阶数。");for(int i=0;i<input.Length;i++){double v=0;for(int k=0;k<fir.Length&&k<=i;k++)v+=fir[k]*input[i-k];Require(Engine.Finite(v),"滤波输出超出浮点范围。");y[i]=v;}return y;}
  var z1=new double[sos.Length];var z2=new double[sos.Length];for(int i=0;i<input.Length;i++){double v=input[i];for(int j=0;j<sos.Length;j++){var a=sos[j];double u=a[0]*v+z1[j];z1[j]=a[1]*v-a[4]*u+z2[j];z2[j]=a[2]*v-a[5]*u;v=u;}Require(Engine.Finite(v),"IIR输出超出浮点范围。");y[i]=v;}return y;
 }
 public static double[] ReadSignal(string path,int column){
  Require(column>=1&&column<=256,"数据列号超出范围。");var result=new List<double>();bool headerAllowed=true;int lineNo=0;
  foreach(string raw in File.ReadLines(path)){lineNo++;string line=raw.Trim();if(line.Length==0||line.StartsWith("#")||line.StartsWith(";"))continue;var parts=line.Contains(",")?line.Split(','):line.Split(new[]{'\t',' ',';'},StringSplitOptions.RemoveEmptyEntries);double v;
   if(parts.Length<column||!double.TryParse(parts[column-1].Trim().Trim('"'),System.Globalization.NumberStyles.Float,Engine.CI,out v)||!Engine.Finite(v)){
    if(headerAllowed&&result.Count==0&&parts.Length>=column&&System.Text.RegularExpressions.Regex.IsMatch(parts[column-1].Trim().Trim('"'),@"^[\p{L}_][\p{L}\p{N}_ ()/.-]*$")&&!new[]{"nan","infinity","inf"}.Contains(parts[column-1].Trim().ToLowerInvariant())){headerAllowed=false;continue;}
    throw new ArgumentException("第"+lineNo+"行第"+column+"列不是有效数值。支持单行标题、逗号/空格/制表符分隔，不支持含分隔符的引号字段。");
   }headerAllowed=false;result.Add(v);Require(result.Count<=65536,"信号最多导入65536个样本。");
  }Require(result.Count>=16,"信号至少需要16个样本。");return result.ToArray();
 }
 public static FilterResult Design(FilterSettings s,double[] imported=null){
  Validate(s);var r=new FilterResult{Config=s.Copy()};if(s.Structure=="FIR"){r.Fir=FirDesign(s);r.ActualOrder=s.Order;}else{r.Sos=IirDesign(s);r.ActualOrder=s.Order*(IsBand(s)?2:1);}
  double[] flat=r.Fir??r.Sos.SelectMany(q=>new[]{q[0],q[1],q[2],q[4],q[5]}).ToArray();
  var quant=new Settings{N=flat.Length,Depth=flat.Length,Width=s.Width,Fraction=s.Fraction,Encoding="Signed",Rounding=s.Rounding,Overflow=s.Overflow};r.Codes=Engine.Quantize(quant,flat);
  if(r.Fir!=null)r.FirQ=r.Codes.Decoded;else{r.SosQ=new double[r.Sos.Length][];for(int i=0;i<r.Sos.Length;i++)r.SosQ[i]=new[]{r.Codes.Decoded[5*i],r.Codes.Decoded[5*i+1],r.Codes.Decoded[5*i+2],1.0,r.Codes.Decoded[5*i+3],r.Codes.Decoded[5*i+4]};}
  r.MaxPole=PoleRadius(r.Sos);r.MaxPoleQ=PoleRadius(r.SosQ);r.Stable=r.MaxPole<1;r.StableQ=r.MaxPoleQ<1;Require(r.Stable,"浮点IIR不稳定，拒绝使用。");
  r.Magnitude=new double[Grid];r.MagnitudeQ=new double[Grid];r.Phase=new double[Grid];r.PhaseQ=new double[Grid];double prev=0,prevQ=0,offset=0,offsetQ=0;
  for(int i=0;i<Grid;i++){double omega=Math.PI*i/(Grid-1);Complex h=Response(r.Fir,r.Sos,omega),q=r.StableQ?Response(r.FirQ,r.SosQ,omega):Complex.Zero;double mag=h.Magnitude,magQ=q.Magnitude;
   Require(Engine.Finite(mag)&&Engine.Finite(magQ),"频响数值不可用；请提高系数精度或调整设计。");r.Magnitude[i]=20*Math.Log10(Math.Max(1e-8,mag));r.MagnitudeQ[i]=20*Math.Log10(Math.Max(1e-8,magQ));r.MaxResponseError=Math.Max(r.MaxResponseError,(h-q).Magnitude);
   double ph=mag<1e-12?prev:h.Phase,pq=magQ<1e-12?prevQ:q.Phase;if(i>0){if(ph-prev>Math.PI)offset-=2*Math.PI;else if(ph-prev< -Math.PI)offset+=2*Math.PI;if(pq-prevQ>Math.PI)offsetQ-=2*Math.PI;else if(pq-prevQ< -Math.PI)offsetQ+=2*Math.PI;}
   r.Phase[i]=(ph+offset)*180/Math.PI;r.PhaseQ[i]=(pq+offsetQ)*180/Math.PI;prev=ph;prevQ=pq;
  }
  var impulse=new double[r.Fir!=null?r.Fir.Length:512];impulse[0]=1;r.Impulse=Apply(impulse,r.Fir,r.Sos);r.ImpulseQ=r.StableQ?Apply(impulse,r.FirQ,r.SosQ):new double[512];
  if(s.TestSignal=="Imported"){Require(imported!=null&&imported.Length>=16,"请先导入信号文件。");Require(imported.Length<=65536&&imported.All(Engine.Finite),"导入信号必须为有限数值，且不超过65536点。");r.Input=(double[])imported.Clone();}else{r.Input=new double[s.SignalLength];for(int i=0;i<r.Input.Length;i++)r.Input[i]=s.TestSignal=="Step"?1:s.TestSignal=="Impulse"?(i==0?1:0):.5*Math.Sin(2*Math.PI*s.Tone1*i/s.Fs)+.5*Math.Sin(2*Math.PI*s.Tone2*i/s.Fs);}
  r.Output=Apply(r.Input,r.Fir,r.Sos);r.OutputQ=r.StableQ?Apply(r.Input,r.FirQ,r.SosQ):new double[r.Input.Length];
  string meaning=s.Structure=="FIR"?"FIR窗函数法边缘约为半幅（-6 dB），不是统一的-3 dB截止。":s.Method=="Butterworth"?"Butterworth边缘为半功率（-3.0103 dB）。":s.Method=="ChebyshevI"?"Chebyshev I边缘为通带纹波边缘（-Rp dB）。":"Chebyshev II边缘为阻带边缘（-Rs dB）。";
  r.Report="结构："+s.Structure+"  响应："+s.Response+"  实际阶数："+r.ActualOrder+"\r\n"+meaning+"\r\n"+(s.Structure=="FIR"?"抽头数="+r.Fir.Length+"；理论线性相位群延迟="+(s.Order/2.0)+" 样本。":"SOS节数="+r.Sos.Length+"；浮点最大极点模="+Engine.F(r.MaxPole)+"；量化后="+Engine.F(r.MaxPoleQ))+"\r\n最大系数量化误差="+Engine.F(r.Codes.MaxError)+"；饱和="+r.Codes.Saturated+"\r\n量化后稳定："+r.StableQ+"；4097点网格最大复频响误差="+(r.StableQ?Engine.F(r.MaxResponseError):"不可用（量化不稳定）")+"\r\n\r\n信号采用零初态、单向因果滤波，未补偿群延迟。橙色结果仅量化系数；运算状态和累加采用double，不是FPGA逐位定点仿真。\r\nFIR脉冲响应显示全部抽头，IIR仅显示前512个样本（不是完整无限响应）。\r\n幅频响应为绝对增益20log10|H|，并非峰值归一化；低于-160dB截到显示下限。零响应附近相位没有物理意义。\r\nIIR分母约定：1+a1 z^-1+a2 z^-2；差分方程反馈使用 -a1*y[n-1]-a2*y[n-2]。\r\nIIR的a0恒为1，导出存储表只含每节[b0,b1,b2,a1,a2]；CSV另含a0。\r\n浮点与量化后频响都应检查；有限网格不构成连续频率指标的严格证明。";
  if(!r.StableQ)r.Report+="\r\n警告：量化后不稳定，禁用系数导出与量化信号处理。请增加W/F或调整阶数/频率。";
  return r;
 }
 public static void ExportCoe(string path,FilterResult r,string format){
  Require(r.StableQ,"量化后滤波器不稳定，禁止导出。");if(format=="FIR COE")Require(r.Fir!=null,"FIR Compiler只接受FIR抽头，不能使用IIR SOS。");
  if(format=="FIR COE"){
   string destination=path;path=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{
   var sb=new StringBuilder();sb.AppendLine("; SignalWorkbench FIR coefficients: signed integers, W="+r.Config.Width+", F="+r.Config.Fraction);sb.AppendLine("; Integer Coefficients mode: q is stored unchanged. Interpret q as q/2^F; account for 2^F in output scaling.");sb.AppendLine("radix=10;");sb.AppendLine("coefdata=");for(int i=0;i<r.Codes.Integers.Length;i++)sb.AppendLine(r.Codes.Integers[i]+(i==r.Codes.Integers.Length-1?";":","));File.WriteAllText(path,sb.ToString(),Encoding.ASCII);
   var data=File.ReadAllText(path);var vals=data.Substring(data.IndexOf("coefdata=",StringComparison.Ordinal)+9).Trim().TrimEnd(';').Split(',');Require(vals.Length==r.Codes.Integers.Length,"FIR COE回读数量错误。");for(int i=0;i<vals.Length;i++)Require(long.Parse(vals[i],Engine.CI)==r.Codes.Integers[i],"FIR COE回读不符。");if(File.Exists(destination))File.Replace(path,destination,null);else File.Move(path,destination);
   }finally{if(File.Exists(path))File.Delete(path);}
  }else{var codes=r.Codes;codes.Config.Platform=format=="Altera MIF"?"Altera":"Vivado";codes.Config.Mode=2;codes.Config.Window=r.Fir!=null?"FIR_h[0..N-1]":"IIR_SOS_[b0,b1,b2,a1,a2]_a0=1";Engine.Export(path,codes);}
 }
 public static void ExportCoefficients(string path,FilterResult r){using(var w=new StreamWriter(path,false,new UTF8Encoding(true))){w.WriteLine("section,index,name,float_coefficient,quantized_coefficient,integer_code,hex");for(int i=0;i<r.Codes.Values.Length;i++){bool fir=r.Fir!=null;string name=fir?"h"+i:new[]{"b0","b1","b2","a1","a2"}[i%5];w.WriteLine((fir?0:i/5)+","+i+","+name+","+Engine.F(r.Codes.Values[i])+","+Engine.F(r.Codes.Decoded[i])+","+r.Codes.Integers[i]+","+Engine.Word(r.Codes.Words[i],16,r.Config.Width));}if(r.Sos!=null)for(int i=0;i<r.Sos.Length;i++)w.WriteLine(i+",,a0,1,1,,");}}
 public static void ExportSignal(string path,FilterResult r){Require(r.StableQ,"量化后不稳定，不能导出量化滤波结果。");using(var w=new StreamWriter(path,false,new UTF8Encoding(true))){w.WriteLine("index,time_s,input,float_filter_output,quantized_coeff_output");for(int i=0;i<r.Input.Length;i++)w.WriteLine(i+","+Engine.F(i/r.Config.Fs)+","+Engine.F(r.Input[i])+","+Engine.F(r.Output[i])+","+Engine.F(r.OutputQ[i]));}}
}
}
