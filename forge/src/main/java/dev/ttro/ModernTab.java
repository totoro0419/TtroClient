package dev.ttro;

import java.util.*;
import java.lang.reflect.Field;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.*;
import net.minecraft.client.network.NetworkPlayerInfo;
import net.minecraft.scoreboard.*;
import net.minecraft.util.IChatComponent;
import net.minecraft.world.WorldSettings;
import net.minecraftforge.fml.relauncher.ReflectionHelper;

/** Display-only view of normal server tablist data. No public API requests. */
public final class ModernTab {
 private static Field header,footer;private static boolean failed;
 private static long cachedAt;private static final List<Row> rows=new ArrayList<Row>();
 static final class Row {String name;int ping;boolean own;Row(String n,int p,boolean o){name=n;ping=p;own=o;}}
 public static boolean supports(int count,boolean objective,boolean headerOrFooter){return count>0&&count<=80&&!objective&&!headerOrFooter;}
 public static boolean render(GuiPlayerTabOverlay vanilla,int width,Scoreboard board,ScoreObjective objective){
  if(Config.data==null||!Config.enabled("moderntab")||failed)return false;
  Minecraft mc=Minecraft.getMinecraft();if(mc.getNetHandler()==null||mc.thePlayer==null)return false;
  try{
   if(header==null){header=ReflectionHelper.findField(GuiPlayerTabOverlay.class,"header","field_175256_i");footer=ReflectionHelper.findField(GuiPlayerTabOverlay.class,"footer","field_175255_h");}
   Collection<NetworkPlayerInfo> infos=mc.getNetHandler().getPlayerInfoMap();
   if(!supports(infos.size(),objective!=null,header.get(vanilla)!=null||footer.get(vanilla)!=null))return false;
   long now=System.currentTimeMillis();if(now-cachedAt>250){cachedAt=now;rows.clear();List<NetworkPlayerInfo> sorted=new ArrayList<NetworkPlayerInfo>(infos);
    Collections.sort(sorted,new Comparator<NetworkPlayerInfo>(){public int compare(NetworkPlayerInfo a,NetworkPlayerInfo b){boolean as=a.getGameType()==WorldSettings.GameType.SPECTATOR,bs=b.getGameType()==WorldSettings.GameType.SPECTATOR;if(as!=bs)return as?1:-1;String at=a.getPlayerTeam()==null?"":a.getPlayerTeam().getRegisteredName(),bt=b.getPlayerTeam()==null?"":b.getPlayerTeam().getRegisteredName();int c=at.compareTo(bt);return c!=0?c:a.getGameProfile().getName().compareToIgnoreCase(b.getGameProfile().getName());}});
    for(NetworkPlayerInfo info:sorted){IChatComponent display=info.getDisplayName();String name=display!=null?display.getFormattedText():ScorePlayerTeam.formatPlayerName(info.getPlayerTeam(),info.getGameProfile().getName());rows.add(new Row(name,info.getResponseTime(),info.getGameProfile().getId().equals(mc.thePlayer.getUniqueID())));}
   }
   int columns=rows.size()>20?Math.min(4,(rows.size()+19)/20):1,per=(rows.size()+columns-1)/columns;int cell=Math.min(190,(width-24)/columns);if(cell<95)return false;int left=(width-cell*columns)/2,top=12;
   Gui.drawRect(left,top,left+cell*columns,top+24+per*18,0xeefffaf0);Gui.drawRect(left,top,left+cell*columns,top+22,0xffe4b74e);
   mc.fontRendererObj.drawString("PLAYERS  /  "+rows.size()+" ONLINE",left+8,top+7,0xff293544);
   for(int i=0;i<rows.size();i++){Row r=rows.get(i);int x=left+(i/per)*cell,y=top+24+(i%per)*18;if(r.own)Gui.drawRect(x+2,y,x+cell-2,y+17,0xfff5e7bd);String ping=r.ping<0?"?":r.ping+" ms";mc.fontRendererObj.drawString(mc.fontRendererObj.trimStringToWidth(r.name,cell-60),x+7,y+5,0xff293544);mc.fontRendererObj.drawString(ping,x+cell-7-mc.fontRendererObj.getStringWidth(ping),y+5,0xff46576b);}
   return true;
  }catch(Exception ex){failed=true;Core.message="Modern Tab unavailable; using Vanilla player list.";return false;}
 }
 public static void reset(){rows.clear();cachedAt=0;}
 private ModernTab(){}
}
