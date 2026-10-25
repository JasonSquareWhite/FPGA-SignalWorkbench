# FPGA SignalWorkbench / FPGA 信号与系数工作台

## 中文说明

轻量 Windows 桌面工具，面向 Vivado 优先的窗函数、波形及滤波器系数生成。当前版本 **1.1.1**，界面为中文。使用 C# WinForms 和系统 .NET Framework 4.8，无需 Python 或 SciPy 运行软件。优先完善滤波器设计与系数导出，已有信号导入/预览为辅助功能。

### 下载与运行

从 [Releases](https://github.com/JasonSquareWhite/FPGA-SignalWorkbench/releases) 下载便携版 ZIP，解压后双击 `SignalWorkbench.exe`。保留同目录的 `SignalWorkbench.exe.config`，用于 DPI 配置。支持 Windows 10/11 和 .NET Framework 4.8。主程序约 100 KiB；依赖 Windows 自带/另行安装的 .NET Framework，不是包含运行时的跨平台单文件应用。未签名的可执行文件不等于恶意文件，但请自行核对发布页 SHA-256，按组织策略运行。

### 已有功能

- 12 种窗：Rectangular、Hann、Hamming、Blackman、Blackman-Harris、Nuttall、Bartlett、Bartlett-Hann、Flat-top、Kaiser、Tukey、Gaussian；对称/周期采样及原始、峰值、和、RMS 归一化。
- 正弦、余弦、三角、方波、升/降锯齿、直流、周期脉冲；点数、频率/周期数、相位、占空比、幅值和加窗。
- FIR 窗函数法；IIR Butterworth、Chebyshev I/II，均支持低通、高通、带通、带阻。FIR 阶数 1～2048；IIR 原型阶数 1～12，带通/带阻最终阶数加倍。
- 系数位宽/小数位、舍入、溢出报错或饱和，量化前后响应、系数表、IIR 量化稳定性检查、参数 XML 保存/载入。
- Vivado ROM COE、Vivado FIR Compiler COE、Altera/Intel MIF、CSV；底部可选择保存位置。
- 辅助的双正弦/脉冲/阶跃测试，以及 CSV/TXT 导入、滤波前后曲线与数据导出。

### 使用与硬件解释

选择功能页，设置参数后点击“计算预览”。检查原始/量化响应与误差，在底部选择保存位置再导出。改变参数会使旧结果失效。默认输出目录为存在的 `D:\FPGA`，否则为程序目录；不含用户私有配置。

定点约定：`q = round(x * 2^F)`，数值还原为 `q / 2^F`。窗口/波形存储表支持有符号补码、无符号和偏移二进制；滤波系数使用有符号补码。

- **ROM COE / MIF** 只初始化存储器；位宽、深度、符号、F 和滤波数据通路必须由硬件工程配置。
- **FIR Compiler COE** 使用 `radix=10; coefdata=...;` 有符号整数。IP 采用 Integer Coefficients 和相同 W；整数乘加输出包含额外 F 个小数位，需正确解释/舍入输出，不能把整数 q 当作原始浮点 h。不要再次自动归一化。
- **IIR** 导出每节 `[b0,b1,b2,a1,a2]`，`a0=1` 隐含；分母 `1+a1*z^-1+a2*z^-2`，反馈项为减号。IIR 系数不能送入 FIR Compiler。
- FIR 截止边缘约为半幅 -6 dB；Butterworth 为半功率 -3.0103 dB；Chebyshev I 为通带纹波边缘，II 为阻带边缘。不能统一解释为 -3 dB。

详细说明：[使用说明](docs/使用说明.txt)。

### 科学性、修复与边界

1.1.1 修复了 Kaiser 极窄过渡带估算的整数溢出；在转换为整数前拒绝超过 2048 的阶数。FIR 脉冲响应现显示全部抽头；IIR 脉冲响应只显示前 512 点。

IIR 边缘到 0 和 Fs/2 的距离、带通/带阻带宽均需至少 `(Fs/2)*0.0001`。这是本双精度实现的保守数值支持范围，不是理论限制。量化后极点模达到/超过 1 时禁止导出。通过检查不代表满足所有通阻带指标，也不保证定点硬件无溢出或极限环。

Kaiser 阶数是经验估算。有限频率网格无法证明全频域指标；零响应处相位无定义。预览量化系数后仍以 double 运算，**不是 FPGA 逐位定点仿真**。理想非正弦波未带限，可能混叠。未实现椭圆滤波器、等波纹 FIR、自动最小 IIR 阶数、filtfilt、HDL 或自动硬件部署。

复查结果：核心 25,552 断言；324 组窗、432 组波形；360 组常规滤波器；143 个新增边界断言及 126 组边界 IIR。常规复频响最大绝对差 `2.495e-10`，边界组 `1.284e-8`，均通过各自记录的容差。参考依赖仅用于测试。[验证记录](docs/验证记录.txt)、[机器可读结果](docs/validation/)。这属于有限测试证据，不是“所有参数都不会有科学性错误”的保证。

2026-10-25 再次通过 Vivado 2022.2 集成检查：ROM 示例完成 XSim 2048 个存储字核对；FIR Compiler 7.2 读取整数 COE 并生成目标，后者未运行数据通路仿真。Quartus 尚未实际集成测试，MIF 只做文件回读检查。

### 从源码构建与测试

在 Windows PowerShell、项目根目录运行：

```powershell
.\build.ps1
# 仅 C# 自测，无 Python 依赖
.\tests\run.ps1 -SkipReference
# 独立科学计算对照（建议 Python 3.12，安装到自己的虚拟环境）
python -m pip install "numpy>=2,<3" "scipy>=1.15,<2"
.\tests\run.ps1 -Python python
```

系统编译器位于 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。测试结果写入 `tests/results/`，不提交版本库。可选 Vivado 检查需要合法安装 Vivado 及相应器件/IP；先运行 C# 自测，再执行 `vivado -mode batch -source tests/vivado_validate.tcl` 或 `tests/vivado_filter_validate.tcl`。脚本仅创建隔离项目，不烧写硬件。GitHub Actions 运行构建和数值测试，不运行商业 Vivado。

### 许可与反馈

公开源代码供查看；目前**未授予开源许可证**，见 [LICENSE](LICENSE)。如需再分发或商用，请先联系仓库所有者。报告问题时提供参数 XML、期望值、实际结果和工具版本，不要上传私有工程或凭据。

### 参考依据

[SciPy firwin](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.firwin.html)、[butter](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.butter.html)、[cheby1](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.cheby1.html)、[cheby2](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.cheby2.html)、[kaiserord](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.kaiserord.html)、[AMD PG149](https://docs.amd.com/r/en-US/pg149-fir-compiler/Filter-Coefficient-Data)。算法为 C# 实现，SciPy 用作独立参考，不随应用分发。

---

## English documentation

A lightweight Windows desktop generator for FPGA windows, waveforms and filter coefficients, prioritizing Vivado. Version **1.1.1** has a Chinese-language interface and uses C# WinForms with .NET Framework 4.8. Python and SciPy are test dependencies only. Filter design and coefficient export are the priority; signal import/preview is auxiliary.

### Download and run

Download the portable ZIP from [Releases](https://github.com/JasonSquareWhite/FPGA-SignalWorkbench/releases), extract it and run `SignalWorkbench.exe`. Keep `SignalWorkbench.exe.config` beside it for DPI configuration. Requires Windows 10/11 and .NET Framework 4.8. The executable is approximately 100 KiB; this excludes the system runtime and is not a cross-platform self-contained application. The executable is unsigned: verify its published SHA-256 and follow your organization's execution policy.

### Features

- Twelve windows: Rectangular, Hann, Hamming, Blackman, Blackman-Harris, Nuttall, Bartlett, Bartlett-Hann, Flat-top, Kaiser, Tukey and Gaussian; symmetric/periodic sampling and raw, peak, sum or RMS normalization.
- Sine, cosine, triangle, square, rising/falling sawtooth, DC and periodic pulse; length, frequency/cycles, phase, duty cycle, amplitude and optional windowing.
- Window-method FIR and Butterworth/Chebyshev I/Chebyshev II IIR; lowpass, highpass, bandpass and bandstop. FIR orders 1–2048; IIR prototype orders 1–12, doubled for bandpass/bandstop.
- Width/fractional-bit selection, rounding, overflow rejection or saturation, original/quantized responses, coefficient tables, quantized IIR stability checks and XML presets.
- Vivado ROM COE, FIR Compiler COE, Altera/Intel MIF and CSV exports with a selectable output directory.
- Auxiliary two-tone, impulse and step tests, CSV/TXT input and filtered signal plots/exports.

### Workflow and hardware interpretation

Select a page, set parameters and calculate the preview. Inspect original/quantized responses and errors, select an output directory and export. Parameter changes invalidate previous results. The default output folder is `D:\FPGA` if present, otherwise the application directory; personal settings are not distributed.

Fixed-point convention: `q = round(x * 2^F)` and decoded value `q / 2^F`. Window/waveform tables support signed two's complement, unsigned and offset binary. Filter coefficients are signed.

- **ROM COE / MIF** initialize memory only. Hardware must define matching width, depth, signedness, F and the filter datapath.
- **FIR Compiler COE** uses signed decimal integers in `radix=10; coefdata=...;`. Use Integer Coefficients with matching W. Integer MAC outputs carry F additional fractional bits; interpret/round the output accordingly. Do not interpret integer q as floating h or normalize it again.
- **IIR** exports `[b0,b1,b2,a1,a2]` per section, with implicit `a0=1`. The denominator is `1+a1*z^-1+a2*z^-2`, so feedback terms are subtracted. These are not FIR Compiler coefficients.
- FIR window-method edges are approximately half-amplitude (-6 dB); Butterworth edges are half-power (-3.0103 dB); Chebyshev I specifies a passband ripple edge and II a stopband edge. They are not interchangeable -3 dB definitions.

The [detailed user guide](docs/使用说明.txt) is in Chinese.

### Scientific validation and limitations

Version 1.1.1 fixes integer overflow in extreme Kaiser order estimation by rejecting orders over 2048 before conversion. FIR impulse plots include all taps; IIR plots show only the first 512 samples.

For IIR, edge distances from DC/Nyquist and bandpass/bandstop bandwidth must be at least `(Fs/2)*0.0001`. This is a conservative numerical support boundary of this double-precision implementation, not a theoretical DSP limit. Quantized pole magnitudes at/above one block export. Passing the checks does not establish every passband/stopband specification or exclude hardware overflow/limit cycles.

Kaiser order estimation is empirical. Finite grids do not prove continuous-frequency specifications; phase at response zeros is undefined. Preview arithmetic remains double after coefficient quantization and is **not bit-accurate FPGA fixed-point simulation**. Ideal nonsinusoidal waveforms are not bandlimited and may alias. Elliptic filters, equiripple FIR, automatic minimum IIR order, filtfilt, HDL generation and hardware deployment are not implemented.

Validation covers 25,552 core assertions, 324 window configurations, 432 waveform configurations, 360 ordinary filter designs, 143 additional boundary assertions and 126 extreme IIR designs. Maximum complex response differences were `2.495e-10` for ordinary designs and `1.284e-8` for boundary designs, within their recorded tolerances. See [validation history](docs/验证记录.txt) and [machine-readable results](docs/validation/). This is finite evidence, not a guarantee of correctness for every input.

Vivado 2022.2 integration checks rerun on 2026-10-25: XSim compared 2048 ROM words for the window/wave examples; FIR Compiler 7.2 imported the integer COE and generated targets, without datapath simulation. Quartus integration has not been run; MIF was checked by file readback only.

### Build and test

From the repository root in Windows PowerShell:

```powershell
.\build.ps1
# C# checks without Python
.\tests\run.ps1 -SkipReference
# Independent numerical reference (Python 3.12 recommended; use your own virtual environment)
python -m pip install "numpy>=2,<3" "scipy>=1.15,<2"
.\tests\run.ps1 -Python python
```

The build uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Generated test results live in ignored `tests/results/`. Optional Vivado checks require a licensed/installed Vivado toolchain and the relevant device/IP support: run C# checks first, then `vivado -mode batch -source tests/vivado_validate.tcl` or `tests/vivado_filter_validate.tcl`. These scripts create isolated projects and do not program hardware. GitHub Actions builds and runs numerical checks, not commercial Vivado.

### Licensing and feedback

Source is publicly visible but **no open-source license has been granted**; see [LICENSE](LICENSE). Contact the owner for redistribution/commercial use. Report issues with preset XML, expected/actual results and tool versions; do not upload private projects or credentials.

### References

[SciPy firwin](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.firwin.html), [butter](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.butter.html), [cheby1](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.cheby1.html), [cheby2](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.cheby2.html), [kaiserord](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.kaiserord.html), [AMD PG149](https://docs.amd.com/r/en-US/pg149-fir-compiler/Filter-Coefficient-Data). Algorithms are implemented in C#; SciPy is an independent reference, not a shipped application dependency.
