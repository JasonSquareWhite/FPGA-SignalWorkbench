`timescale 1ns/1ps
module rom_tb;
reg clk=0;
reg [9:0] addr=0;
wire [15:0] hann_data, sine_data;
reg [15:0] hann_expected [0:1023];
reg [15:0] sine_expected [0:1023];
integer i;
always #5 clk=~clk;
hann_rom h(.clka(clk), .addra(addr), .douta(hann_data));
sine_rom s(.clka(clk), .addra(addr), .douta(sine_data));
initial begin
  $readmemh("hann_expected.mem", hann_expected);
  $readmemh("sine_expected.mem", sine_expected);
  for(i=0;i<1024;i=i+1) begin
    @(negedge clk); addr=i;
    @(posedge clk); #1;
    if(hann_data !== hann_expected[i]) $fatal(1,"HANN mismatch addr=%d actual=%h expected=%h",i,hann_data,hann_expected[i]);
    if(sine_data !== sine_expected[i]) $fatal(1,"SINE mismatch addr=%d actual=%h expected=%h",i,sine_data,sine_expected[i]);
  end
  $display("PASS: VIVADO_BMG_XSIM_ALL_2048_WORDS_MATCH_SCIPY");
  $finish;
end
initial begin #19000; $fatal(1,"Simulation timeout"); end
endmodule
