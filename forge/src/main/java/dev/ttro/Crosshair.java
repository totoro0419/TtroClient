package dev.ttro;
import net.minecraft.client.Minecraft;
import net.minecraftforge.client.event.RenderGameOverlayEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
/** Rendering-only listener. Enabling a crosshair never starts HUD data polling. */
public final class Crosshair {
 @SubscribeEvent public void pre(RenderGameOverlayEvent.Pre e){if(e.type==RenderGameOverlayEvent.ElementType.CROSSHAIRS&&Config.enabled("crosshair")){e.setCanceled(true);int x=e.resolution.getScaledWidth()/2,y=e.resolution.getScaledHeight()/2,size=(int)Config.number("crosshair","size"),gap=(int)Config.number("crosshair","gap");Minecraft m=Minecraft.getMinecraft();int color=Config.bool("crosshair","target")&&m.objectMouseOver!=null&&m.objectMouseOver.entityHit!=null?0xffffb02e:0xffffffff;String shape=Config.text("crosshair","shape");OverlayBatch.begin();try{if(shape.equals("dot"))OverlayBatch.rect(x-1,y-1,x+2,y+2,color);else{OverlayBatch.rect(x-gap-size,y,x-gap,y+1,color);OverlayBatch.rect(x+gap,y,x+gap+size,y+1,color);OverlayBatch.rect(x,y+gap,x+1,y+gap+size,color);if(!shape.equals("t"))OverlayBatch.rect(x,y-gap-size,x+1,y-gap,color);}}finally{OverlayBatch.end();}}}
}
