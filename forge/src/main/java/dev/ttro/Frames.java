package dev.ttro;
import java.io.*;
import net.minecraftforge.fml.common.gameevent.TickEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import net.minecraft.client.Minecraft;
/** Explicit QA recording only; never present as an active listener during normal play. */
public final class Frames {
 private final long[] samples=new long[3600];private long last;private int index;
 @SubscribeEvent public void tick(TickEvent.RenderTickEvent e){if(e.phase!=TickEvent.Phase.END)return;long now=System.nanoTime();if(last!=0&&index<samples.length)samples[index++]=now-last;last=now;
  if(index==samples.length){File f=new File(Minecraft.getMinecraft().mcDataDir,"ttro-frames.csv");try(PrintWriter w=new PrintWriter(f)){w.println("frame,frametime_ms");for(int i=0;i<samples.length;i++)w.println(i+","+(samples[i]/1000000.0));}catch(IOException ex){Core.message="Frame capture failed.";}net.minecraftforge.fml.common.FMLCommonHandler.instance().bus().unregister(this);}
 }
}
