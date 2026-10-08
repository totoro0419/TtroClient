package dev.ttro;
import java.util.*;
import net.minecraft.client.Minecraft;
/** Classification is a policy decision, not a guarantee against punishments. */
public final class Safety {
 public static boolean hypixel=false;
 private static final Set<String> BLOCKED=new HashSet<String>(Arrays.asList("inventory","togglesprint","togglesneak","reach","freelook","dropprotection","combo"));
 public static void update(){Minecraft m=Minecraft.getMinecraft();String host=m.getCurrentServerData()==null?"":m.getCurrentServerData().serverIP.toLowerCase(Locale.ROOT).split(":")[0];boolean next=host.equals("hypixel.net")||host.endsWith(".hypixel.net")||HypixelBridge.officialHello;
  if(hypixel!=next){hypixel=next;Core.apply();}
 }
 public static boolean allowed(String id){return !hypixel||!BLOCKED.contains(id);}
 public static String reason(String id){return allowed(id)?"Not blocked by this profile; no guarantee":"Hypixel: restricted / uncertain. Disabled for this session.";}
 private Safety(){}
}
