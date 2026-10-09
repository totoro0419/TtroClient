package dev.ttro;
import java.lang.reflect.*;
import java.util.function.Consumer;
/** Official Mod API only. No Public API key is accepted. */
public final class HypixelBridge {
 public static boolean officialHello;public static String location="Unknown",party="Not requested";private static Object api;private static boolean initialized;
 public static void init(){if(initialized||!Config.enabled("hypixel"))return;
  try{Class<?> c=Class.forName("net.hypixel.modapi.HypixelModAPI");api=c.getMethod("getInstance").invoke(null);
   handler("net.hypixel.modapi.packet.impl.clientbound.ClientboundHelloPacket",packet->{if(Config.enabled("hypixel")){officialHello=true;Safety.update();}});
   Class<?> loc=Class.forName("net.hypixel.modapi.packet.impl.clientbound.event.ClientboundLocationPacket");c.getMethod("subscribeToEventPacket",Class.class).invoke(api,loc);
   handler(loc.getName(),packet->{if(Config.enabled("hypixel")){location=packet.toString();Core.message=location;}});
   handler("net.hypixel.modapi.packet.impl.clientbound.ClientboundPartyInfoPacket",packet->{if(Config.enabled("hypixel")){party=packet.toString();Core.message=party;}});initialized=true;
  }catch(Exception e){Core.message="Official Hypixel Mod API not installed / unavailable.";api=null;}
 }
 private static void handler(String packet,Consumer<Object> consume)throws Exception {
  Class<?> handler=Class.forName("net.hypixel.modapi.handler.ClientboundPacketHandler");Object callback=Proxy.newProxyInstance(handler.getClassLoader(),new Class<?>[]{handler},(proxy,method,args)->{if(method.getDeclaringClass()==Object.class){if(method.getName().equals("hashCode"))return System.identityHashCode(proxy);if(method.getName().equals("equals"))return proxy==args[0];return "Ttro Client official API handler";}if(method.getName().equals("handle")&&args!=null&&args.length>0){final Object received=args[0];net.minecraft.client.Minecraft.getMinecraft().addScheduledTask(()->consume.accept(received));}return null;});
  api.getClass().getMethod("registerHandler",Class.class,handler).invoke(api,Class.forName(packet),callback);
 }
 public static void requestParty(){if(!Config.enabled("hypixel")||!Config.bool("hypixel","party"))return;init();if(api==null)return;try{Object p=Class.forName("net.hypixel.modapi.packet.impl.serverbound.ServerboundPartyInfoPacket").newInstance();boolean sent=(Boolean)api.getClass().getMethod("sendPacket",Class.forName("net.hypixel.modapi.packet.HypixelPacket")).invoke(api,p);Core.message=sent?"Party request sent via official Mod API.":"Party unavailable: server/API handshake required.";}catch(Exception e){Core.message="Party information unavailable.";}}
 private HypixelBridge(){}
}
