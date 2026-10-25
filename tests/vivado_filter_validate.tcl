# Isolated FIR Compiler import/generation check. No board or user project.
set root [file normalize [file join [file dirname [info script]] ..]]
set out [file join $root validation fir_vivado]
file mkdir $out
create_project fir_validation [file join $out project] -part xc7a35tcpg236-1 -force
create_ip -name fir_compiler -vendor xilinx.com -library ip -version 7.2 -module_name fir_check
set_property -dict [list CONFIG.CoefficientSource {COE_File} CONFIG.Coefficient_File [file join $root examples fir_lowpass_w18_f16.coe] CONFIG.Quantization {Integer_Coefficients} CONFIG.Coefficient_Width {18} CONFIG.Data_Width {16}] [get_ips fir_check]
generate_target all [get_ips fir_check]
set f [open [file join $out import_pass.txt] w]
puts $f "PASS: FIR Compiler 7.2 accepted signed integer COE and generated targets. W=18; application coefficient scale=2^16; integer IP output must be interpreted with this scale. This check does not simulate the datapath."
close $f
close_project
exit
