package dev.ttro;
import java.util.*;
import java.text.SimpleDateFormat;
import com.google.gson.JsonObject;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.*;
import net.minecraft.client.renderer.GlStateManager;
import net.minecraft.item.ItemStack;
import net.minecraft.potion.Potion;
import net.minecraft.potion.PotionEffect;
import net.minecraft.client.network.NetworkPlayerInfo;
import net.minecraftforge.client.event.RenderGameOverlayEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import net.minecraftforge.fml.common.gameevent.TickEvent;
import org.lwjgl.input.Keyboard;

/** Tick-built text; render reuses fixed entries and never rebuilds the model. */
public final class Hud {
 public static final String[] ELEMENT_IDS={"fps","ping","cps","keystrokes","bps","coordinates","memory","armor","potions","items","clock","connection"};
 static final Clicks left=new Clicks(),right=new Clicks();
 static final class Entry {
  final String id,label; final String[] lines=new String[48]; int count;
  Entry(String id,String label){this.id=id;this.label=label;}
  void clear(){Arrays.fill(lines,null);count=0;}
  void add(String value){if(count<lines.length)lines[count++]=value;}
  void append(Entry entry){for(int i=0;i<entry.count;i++)add(entry.lines[i]);}
 }
 final Entry[] elements={new Entry("fps","FPS"),new Entry("ping","Ping"),new Entry("cps","CPS L / R"),new Entry("keystrokes","Keystrokes"),new Entry("bps","BPS"),new Entry("coordinates","Coordinates"),new Entry("memory","Memory"),new Entry("armor","Armor / Durability"),new Entry("potions","Potion Effects"),new Entry("items","Item counters"),new Entry("clock","Clock"),new Entry("connection","Connection Quality")};
 private static final int[][] GROUP_MEMBERS={{2,3,4},{7,8},{1,11},{0,5,6,9,10}};
 private final Entry[] groups={new Entry("combat","COMBAT"),new Entry("status","STATUS"),new Entry("network","NETWORK"),new Entry("game","GAME")};
 private final double[] pings=new double[120];private int count,index,lastPing=-1,window;private Object world;
 private final SimpleDateFormat clockFormat=new SimpleDateFormat("HH:mm");private long minute=-1;private String clock="";
 static boolean anyEnabled(){for(String id:ELEMENT_IDS)if(Config.enabled(id))return true;return false;}
 void reset(){for(Entry entry:elements)entry.clear();for(Entry entry:groups)entry.clear();count=index=0;lastPing=-1;world=null;left.clear();right.clear();}
 @SubscribeEvent public void tick(TickEvent.ClientTickEvent e){if(e.phase==TickEvent.Phase.END&&Core.ticks%2==0)refresh();}
 @SubscribeEvent public void mouse(net.minecraftforge.client.event.MouseEvent e){if(e.buttonstate&&Config.enabled("cps")){if(e.button==0)left.click();if(e.button==1)right.click();}}
 public void refresh(){if(Config.data==null)return;Minecraft m=Minecraft.getMinecraft();if(world!=m.theWorld){reset();world=m.theWorld;}for(Entry entry:elements)entry.clear();for(Entry entry:groups)entry.clear();if(m.thePlayer==null)return;
  boolean focus=Config.enabled("adaptivehud")&&Config.bool("adaptivehud","combatFocus")&&System.currentTimeMillis()<Core.combatUntil;
  if(Config.enabled("fps"))elements[0].add(Minecraft.getDebugFPS()+" FPS");
  boolean network=Config.enabled("ping")||Config.enabled("connection");NetworkPlayerInfo info=!network||m.getNetHandler()==null?null:m.getNetHandler().getPlayerInfo(m.thePlayer.getUniqueID());boolean missing=info==null||m.isSingleplayer();int ping=missing?-1:info.getResponseTime();
  if(Config.enabled("ping"))elements[1].add(missing?"Ping: unavailable":ping+" ms (server tablist)");
  if(Config.enabled("cps"))elements[2].add(left.value()+" / "+right.value()+" CPS");
  if(Config.enabled("keystrokes"))elements[3].add("W "+key(m.gameSettings.keyBindForward.getKeyCode())+"  A "+key(m.gameSettings.keyBindLeft.getKeyCode())+"  S "+key(m.gameSettings.keyBindBack.getKeyCode())+"  D "+key(m.gameSettings.keyBindRight.getKeyCode()));
  if(Config.enabled("bps")){long hundredths=Math.round(Math.hypot(m.thePlayer.posX-m.thePlayer.prevPosX,m.thePlayer.posZ-m.thePlayer.prevPosZ)*2000);elements[4].add((hundredths/100)+"."+(hundredths%100<10?"0":"")+(hundredths%100)+" BPS");}
  if(Config.enabled("armor"))for(ItemStack stack:m.thePlayer.inventory.armorInventory)if(stack!=null)elements[7].add(stack.getDisplayName()+" "+(stack.isItemStackDamageable()?(stack.getMaxDamage()-stack.getItemDamage())+"/"+stack.getMaxDamage():"-"));
  if(Config.enabled("potions"))for(PotionEffect potion:m.thePlayer.getActivePotionEffects())elements[8].add(net.minecraft.client.resources.I18n.format(Potion.potionTypes[potion.getPotionID()].getName())+" "+(potion.getAmplifier()+1)+" • "+Potion.getDurationString(potion));
  if(Config.enabled("connection")&&!missing){int nextWindow=(int)Config.number("connection","window");if(window!=nextWindow){count=index=0;lastPing=-1;window=nextWindow;}if(ping!=lastPing){pings[index]=ping;index=(index+1)%window;count=Math.min(count+1,window);lastPing=ping;}
   if(count<3)elements[11].add("Network: collecting latency changes");else{double min=Double.MAX_VALUE,max=0,sum=0;for(int i=0;i<count;i++){min=Math.min(min,pings[i]);max=Math.max(max,pings[i]);sum+=pings[i];}double variance=0;for(int i=0;i<count;i++){double delta=pings[i]-sum/count;variance+=delta*delta;}elements[11].add("Tab ping deviation "+Math.round(Math.sqrt(variance/count))+" ms / spike "+Math.round(max-min));}}
  if(!focus){
   if(Config.enabled("coordinates"))elements[5].add("XYZ "+Math.round(m.thePlayer.posX)+" "+Math.round(m.thePlayer.posY)+" "+Math.round(m.thePlayer.posZ));
   if(Config.enabled("memory")){Runtime runtime=Runtime.getRuntime();elements[6].add("RAM "+(runtime.totalMemory()-runtime.freeMemory())/1048576+" / "+runtime.maxMemory()/1048576+" MiB");}
   if(Config.enabled("items")){int blocks=0,arrows=0,pearls=0;for(ItemStack stack:m.thePlayer.inventory.mainInventory)if(stack!=null){if(stack.getItem() instanceof net.minecraft.item.ItemBlock)blocks+=stack.stackSize;if(stack.getItem()==net.minecraft.init.Items.arrow)arrows+=stack.stackSize;if(stack.getItem()==net.minecraft.init.Items.ender_pearl)pearls+=stack.stackSize;}elements[9].add("Blocks "+blocks+" • Arrows "+arrows+" • Pearls "+pearls);}
   if(Config.enabled("clock")){long now=System.currentTimeMillis();if(now/60000!=minute){minute=now/60000;clock=clockFormat.format(new Date(now));}elements[10].add(clock);}
  }
  for(int i=0;i<GROUP_MEMBERS.length;i++)group(i,GROUP_MEMBERS[i]);
 }
 private void group(int target,int[] members){Entry group=groups[target];for(int member:members)if(elements[member].count>0){if(group.count==0)group.add(group.label);group.append(elements[member]);}if(group.count==2){group.lines[0]=group.label+" • "+group.lines[1];group.lines[1]=null;group.count=1;}}
 private static String key(int key){return key>=0&&Keyboard.isKeyDown(key)?"●":"·";}
 @SubscribeEvent public void draw(RenderGameOverlayEvent.Post e){if(e.type!=RenderGameOverlayEvent.ElementType.ALL||Minecraft.getMinecraft().gameSettings.showDebugInfo||Minecraft.getMinecraft().currentScreen instanceof HudEditorScreen)return;drawLayout(e.resolution,false,-1);}
 void drawLayout(ScaledResolution resolution,boolean editor,int selected){JsonObject hud=Config.data.getAsJsonObject("hud");if(hud.get("layout").getAsString().equals("individual")){for(int i=0;i<elements.length;i++){Entry entry=elements[i];if(entry.count==0&&!editor)continue;JsonObject position=hud.getAsJsonObject("elements").getAsJsonObject(entry.id);render(entry,position.get("x").getAsFloat(),position.get("y").getAsFloat(),position.get("scale").getAsFloat(),resolution,editor&&selected==i,editor);}}else{float scale=hud.get("scale").getAsFloat(),x=hud.get("x").getAsFloat(),y=hud.get("y").getAsFloat();int height=0;for(Entry entry:groups)if(entry.count>0)height+=entry.count*11+6;y=Math.max(0,Math.min(y,resolution.getScaledHeight()-height*scale));for(Entry entry:groups)if(entry.count>0){render(entry,x,y,scale,resolution,editor,false);y+=(entry.count*11+6)*scale;}if(editor&&height==0)render(groups[0],x,y,scale,resolution,true,true);}}
 int entryWidth(int i){Entry entry=elements[i];FontRenderer font=Minecraft.getMinecraft().fontRendererObj;int width=font.getStringWidth(entry.label);for(int n=0;n<entry.count;n++)width=Math.max(width,font.getStringWidth(entry.lines[n]));return width+8;}
 private void render(Entry entry,float x,float y,float scale,ScaledResolution resolution,boolean selected,boolean placeholder){int width=0;FontRenderer font=Minecraft.getMinecraft().fontRendererObj;for(int i=0;i<entry.count;i++)width=Math.max(width,font.getStringWidth(entry.lines[i]));if(entry.count==0&&placeholder)width=font.getStringWidth(entry.label);int rows=Math.max(1,entry.count);x=Math.max(0,Math.min(x,resolution.getScaledWidth()-(width+8)*scale));y=Math.max(0,Math.min(y,resolution.getScaledHeight()-(rows*11+4)*scale));GlStateManager.pushMatrix();GlStateManager.translate(x,y,0);GlStateManager.scale(scale,scale,1);Gui.drawRect(0,0,width+8,rows*11+4,selected?0xe0365fd0:0xb02a3039);if(selected)Gui.drawRect(1,1,width+7,rows*11+3,0xe02a3039);for(int i=0;i<rows;i++){String line=entry.count==0?entry.label:entry.lines[i];font.drawString(line,4,3+i*11,i==0&&entry.count>0&&line.equals(entry.label)?0xffefc66d:0xffedf0f2);}GlStateManager.popMatrix();GlStateManager.color(1,1,1,1);}
 static final class Clicks {private final long[] times=new long[256];private int index;void click(){times[index++&255]=System.nanoTime();}void clear(){Arrays.fill(times,0);index=0;}int value(){long min=System.nanoTime()-1000000000L;int n=0;for(long time:times)if(time>min)n++;return n;}}
}
