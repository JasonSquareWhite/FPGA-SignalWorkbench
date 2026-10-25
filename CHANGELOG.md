# Changelog / 更新记录

## 1.1.1 — 2026-10-25

- 修复 Kaiser 极窄过渡带阶数转换溢出；Fix overflow before converting estimated Kaiser order.
- 限制不可靠的极端 IIR 频率；Reject IIR parameters outside the supported numerical range.
- FIR 脉冲响应显示全部抽头；Display the complete FIR impulse response.
- 增加边界回归和独立对照；Add boundary regression and independent reference checks.
- 双语文档及可复现构建测试；Bilingual documentation and reproducible build/test commands.

## 1.1 — 2026-10-22

- 加入 FIR/IIR 设计、量化、系数导出及辅助信号预览。Added filter design, quantization, coefficient export and auxiliary signal preview.
