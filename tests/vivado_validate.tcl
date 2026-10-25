# Isolated Project Mode validation. No user project or board is accessed.
set root [file normalize [file join [file dirname [info script]] ..]]
set checkdir [file join $root validation vivado]
file mkdir $checkdir
create_project signal_validation [file join $checkdir project] -part xc7a35tcpg236-1 -force
foreach pair {{hann_rom hann_1024_w16_f15} {sine_rom sine_1024_w16_f15}} {
    lassign $pair name stem
    create_ip -name blk_mem_gen -vendor xilinx.com -library ip -version 8.4 -module_name $name
    set_property -dict [list CONFIG.Memory_Type {Single_Port_ROM} CONFIG.Write_Width_A {16} CONFIG.Read_Width_A {16} CONFIG.Write_Depth_A {1024} CONFIG.Enable_A {Always_Enabled} CONFIG.Register_PortA_Output_of_Memory_Primitives {false} CONFIG.Register_PortA_Output_of_Memory_Core {false} CONFIG.Load_Init_File {true} CONFIG.Coe_File [file join $root tests results ${stem}.coe]] [get_ips $name]
    generate_target all [get_ips $name]
}
add_files -fileset sim_1 [file join $root tests rom_tb.v]
add_files -fileset sim_1 [list [file join $root tests hann_expected.mem] [file join $root tests sine_expected.mem]]
set_property top rom_tb [get_filesets sim_1]
set_property xsim.simulate.runtime {20us} [get_filesets sim_1]
launch_simulation
close_sim
close_project
puts "SIGNAL_WORKBENCH_VIVADO_FLOW_DONE"
exit
