package dev.ttro;
import java.util.*;
import java.util.regex.*;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.*;
import net.minecraft.scoreboard.*;
/** A strict semantic model. Unknown content keeps the entire original renderer. */
public final class Scoreboard {
 public static final class TeamInfo {public final String name,status;public final boolean own;TeamInfo(String n,String s,boolean o){name=n;status=s;own=o;}}
 public static final class Panel {public String nextEvent="",timer="";public final List<TeamInfo> teams=new ArrayList<TeamInfo>();public final Map<String,Integer> own=new LinkedHashMap<String,Integer>();}
 private static final Pattern EVENT=Pattern.compile("((?:Diamond|Emerald) [IVX]+) in (\\d+):([0-5]\\d)");
 private static final Pattern TEAM=Pattern.compile("[RBGYWAPS] (Red|Blue|Green|Yellow|White|Aqua|Pink|Gray): (.+)");
 private static final Pattern COUNTER=Pattern.compile("(Kills|Final Kills|Beds Broken): (\\d+)");
 public static Panel parse(String title,List<String> rows){
  if(!strip(title).equals("BED WARS"))return null;Panel panel=new Panel();Set<String> names=new HashSet<String>();int meaningful=0;
  for(String raw:rows){String value=strip(raw).trim();if(value.isEmpty()||value.equals("www.hypixel.net")||value.matches("\\d{2}/\\d{2}/\\d{2}.*"))continue;
   Matcher event=EVENT.matcher(value);if(event.matches()){if(!panel.nextEvent.isEmpty())return null;panel.nextEvent=event.group(1);panel.timer=(event.group(2).length()==1?"0":"")+event.group(2)+":"+event.group(3);meaningful++;continue;}
   Matcher team=TEAM.matcher(value);if(team.matches()){String state=team.group(2).trim();boolean own=state.endsWith(" YOU");if(own)state=state.substring(0,state.length()-4).trim();String status;
    if(state.equals("✔")||state.equals("✓"))status="BED INTACT";else if(state.equals("ALIVE"))status="ALIVE";else if(state.equals("✘")||state.equals("✗")||state.equals("X")||state.equals("OUT"))status="OUT";else if(state.matches("[1-4]"))status=state+" ALIVE / BED OUT";else return null;
    if(!names.add(team.group(1)))return null;panel.teams.add(new TeamInfo(team.group(1),status,own));meaningful++;continue;}
   Matcher counter=COUNTER.matcher(value);if(counter.matches()){String label=counter.group(1).equals("Final Kills")?"Finals":counter.group(1).equals("Beds Broken")?"Beds":"Kills";if(panel.own.containsKey(label))return null;try{panel.own.put(label,Integer.parseInt(counter.group(2)));}catch(NumberFormatException invalid){return null;}meaningful++;continue;}
   return null;
  }
  return panel.teams.size()>=2&&meaningful>=3?panel:null;
 }
 public static String strip(String value){return value.replaceAll("§[0-9a-fk-or]","");}
 private static long nextUpdate;private static ScoreObjective cachedObjective;private static Panel cachedPanel;
 public static boolean render(ScoreObjective objective,ScaledResolution resolution){
  if(!Core.scoreboardEnabled||objective==null)return false;long now=System.currentTimeMillis();
  if(cachedObjective!=objective||now>=nextUpdate){nextUpdate=now+200;cachedObjective=objective;net.minecraft.scoreboard.Scoreboard board=objective.getScoreboard();ArrayList<String> rows=new ArrayList<String>();for(Score score:board.getSortedScores(objective)){String name=score.getPlayerName();if(name!=null&&!name.startsWith("#"))rows.add(ScorePlayerTeam.formatPlayerName(board.getPlayersTeam(name),name));}if(rows.size()>15)rows=new ArrayList<String>(rows.subList(rows.size()-15,rows.size()));Collections.reverse(rows);cachedPanel=parse(objective.getDisplayName(),rows);}
  Panel panel=cachedPanel;if(panel==null)return false;boolean combat=Config.bool("scoreboard","adaptive")&&System.currentTimeMillis()<Core.combatUntil;
  FontRenderer font=Minecraft.getMinecraft().fontRendererObj;int width=178;for(TeamInfo team:panel.teams)width=Math.max(width,font.getStringWidth(team.name+(team.own?" (YOU)":""))+font.getStringWidth(team.status)+30);width=Math.min(width,resolution.getScaledWidth()-24);
  int ownRows=combat?0:panel.own.size(),height=25+(panel.nextEvent.isEmpty()?0:21)+17+panel.teams.size()*13+(ownRows>0?19+ownRows*13:0),x=resolution.getScaledWidth()-width-8,y=Math.max(8,resolution.getScaledHeight()/2-height/2);
  OverlayBatch.begin();OverlayBatch.rect(x,y,x+width,y+height,0xdd2a3039);OverlayBatch.rect(x,y,x+3,y+height,0xffd9a749);OverlayBatch.end();font.drawString("BED WARS",x+10,y+9,0xffefc66d);if(combat)right(font,"COMBAT",x+width-9,y+9,0xffbac5cd);int row=y+26;
  if(!panel.nextEvent.isEmpty()){font.drawString(panel.nextEvent,x+10,row,0xffedf0f2);right(font,panel.timer,x+width-9,row,0xffedf0f2);row+=21;}
  font.drawString("TEAMS",x+10,row,0xffaebbc6);row+=16;
  for(int i=0;i<panel.teams.size();i++){TeamInfo team=panel.teams.get(i);String label=team.name+(team.own?" (YOU)":"");font.drawString(label,x+10,row,0xffedf0f2);right(font,team.status,x+width-9,row,team.status.equals("OUT")?0xffa4aeb8:0xffd9eabc);row+=13;}
  if(ownRows>0){row+=4;font.drawString("YOU",x+10,row,0xffaebbc6);row+=15;for(Map.Entry<String,Integer> metric:panel.own.entrySet()){font.drawString(metric.getKey(),x+10,row,0xffedf0f2);right(font,metric.getValue().toString(),x+width-9,row,0xffedf0f2);row+=13;}}
  return true;
 }
 private static void right(FontRenderer font,String text,int edge,int y,int color){font.drawString(text,edge-font.getStringWidth(text),y,color);}
 private Scoreboard(){}
}
