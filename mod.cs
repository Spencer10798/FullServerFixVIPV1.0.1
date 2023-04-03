/**
* <author>Christophe Roblin</author>
* <email>lifxmod@gmail.com</email>
* <url>lifxmod.com</url>
* <credits></credits>
* <description>Disconnects user on preConnect if server is full</description>
* <license>GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007</license>
*/

if (!isObject(LiFxFullServerFixVIPVIP))
{
    new ScriptObject(LiFxFullServerFixVIP)
    {
    };
}
if(!isObject($LiFx::FullServerFixIdleTimeout))
  $LiFx::FullServerFixIdleTimeout = 10;

package LiFxFullServerFixVIP
{
  function LiFxFullServerFixVIP::setup() {
    LiFx::registerCallback($LiFx::hooks::onPostConnectRoutineCallbacks, onPostConnectRequest, LiFxFullServerFixVIP);
    LiFx::registerCallback($LiFx::hooks::onInitServerDBChangesCallbacks, dbInit, LiFxFullServerFixVIP);
    LiFx::registerCallback($LiFx::hooks::onConnectCallbacks,onConnectClient, LiFxFullServerFixVIP);
  }
  
  function LiFxFullServerFixVIP::dbInit() {
    dbi.Update("ALTER TABLE `account` ADD COLUMN `VIPFlag` TINYINT NULL DEFAULT NULL AFTER `SteamID`;");
    dbi.Update("ALTER TABLE `character` ADD COLUMN `LastUpdated` TIMESTAMP NULL DEFAULT NULL AFTER `DeleteTimestamp`");
    dbi.Update("DROP TRIGGER IF EXISTS `character_before_update`;");
    %character_before_update = "CREATE TRIGGER `character_before_update` BEFORE UPDATE ON `character`; FOR EACH ROW BEGIN\n";
    %character_before_update = %character_before_update @ "IF(NEW.GeoID != OLD.GeoID OR NEW.GeoAlt != OLD.GeoAlt) THEN\n";
    %character_before_update = %character_before_update @ "SET NEW.LastUpdated = CURRENT_TIMESTAMP;\n";
    %character_before_update = %character_before_update @ "END IF;\n";
    %character_before_update = %character_before_update @ "END\n";
    dbi.Update(%character_before_update);

    dbi.Update("DROP TRIGGER IF EXISTS `items_after_update`;");
    %items_after_update = "CREATE TRIGGER `items_after_update` AFTER UPDATE ON `items` FOR EACH ROW BEGIN\n";
    %items_after_update = %items_after_update @ "UPDATE `character` chr\n";
    %items_after_update = %items_after_update @ "INNER JOIN (\n";
	  %items_after_update = %items_after_update @ "SELECT lc.id FROM `items` i\n";
		%items_after_update = %items_after_update @ "LEFT JOIN `containers` co ON i.ContainerID = co.ID\n";
		%items_after_update = %items_after_update @ "LEFT JOIN `character` ch ON co.ID = ch.RootContainerID\n";
		%items_after_update = %items_after_update @ "LEFT JOIN `lifx_character` lc ON lc.id = ch.ID\n";
	  %items_after_update = %items_after_update @ "WHERE i.ID = OLD.ID\n";
	  %items_after_update = %items_after_update @ "LIMIT 1\n";
	  %items_after_update = %items_after_update @ ") as id ON id.id = chr.ID\n";
	  %items_after_update = %items_after_update @ "SET chr.LastUpdated = CURRENT_TIMESTAMP;\n";
    %items_after_update = %items_after_update @ "END\n";
    dbi.Update(%items_after_update);

    dbi.Update("DROP TRIGGER IF EXISTS `items_before_update`;");
    %items_before_update = "CREATE TRIGGER `items_before_update` BEFORE UPDATE ON `items` FOR EACH ROW BEGIN\n";
    %items_before_update = %items_before_update @ "UPDATE `character` chr\n";
    %items_before_update = %items_before_update @ "INNER JOIN (\n";
	  %items_before_update = %items_before_update @ "SELECT lc.id FROM `items` i\n";
		%items_before_update = %items_before_update @ "LEFT JOIN `containers` co ON i.ContainerID = co.ID\n";
		%items_before_update = %items_before_update @ "LEFT JOIN `character` ch ON co.ID = ch.RootContainerID\n";
		%items_before_update = %items_before_update @ "LEFT JOIN `lifx_character` lc ON lc.id = ch.ID\n";
	  %items_before_update = %items_before_update @ "WHERE i.ID = OLD.ID\n";
	  %items_before_update = %items_before_update @ "LIMIT 1\n";
	  %items_before_update = %items_before_update @ ") as id ON id.id = chr.ID\n";
	  %items_before_update = %items_before_update @ "SET chr.LastUpdated = CURRENT_TIMESTAMP;\n";
    %items_before_update = %items_before_update @ "END\n";
    dbi.Update(%items_before_update);
  }
  function LiFxFullServerFixVIP::version() {
    return "1.5.1.VIP";
  }

  function LiFxFullServerFixVIP::onConnectClient(%this, %client) {
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE id=" @ %client.getCharacterId());
  }
  function LiFxFullServerFixVIP::onPostConnectRequest(%this, %client, %nettAddress, %name) {
    %client.ConnectedTime = getUnixTime();
    if ($Server::PlayerCount > $Server::MaxPlayers)
    {
        LiFxFullServerFixVIP.ConReq = new ScriptObject() {
          Client = %client;
          NettAddress = %nettAddress;
          Name = %name;
        };
        %client.ConnectedTime = getUnixTime();
        dbi.Select(LiFxFullServerFixVIP, "VIPCheck", "SELECT a.VIPFlag AS VIPFlag, c.ID AS ClientId, lc.active as Active, (SELECT aaa.VIPFlag FROM `account` aaa LEFT JOIN `character` cc ON cc.AccountID = aaa.ID LEFT JOIN `lifx_character` lfccc ON lfccc.id = cc.ID WHERE aaa.ID = " @ %client.getAccountId() @ " AND lfccc.active = 1 AND aaa.VIPFlag = 1) ClientVIP FROM `lifx_character` lc LEFT JOIN `character` c ON c.ID = lc.id LEFT JOIN `account` a ON a.ID = " @ %client.getAccountId() @ " LEFT JOIN `account` ca ON ca.ID = c.AccountID WHERE TIMESTAMPDIFF(MINUTE,c.LastUpdated,CURRENT_TIMESTAMP) > " @ $LiFx::FullServerFixIdleTimeout @ " ORDER BY ca.VIPFlag ASC, lc.active DESC, TIMESTAMPDIFF(MINUTE,c.LastUpdated,CURRENT_TIMESTAMP) DESC LIMIT 1");
    }
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE AccountID=" @ %client.getAccountId());
  }

  function LiFxFullServerFixVIP::VIPCheck(%this,%rs) {
    if(%rs.ok() && %rs.nextRecord())
    {
      %VIPFlag = %rs.getFieldValue("VIPFlag");
      %ClientID = %rs.getFieldValue("ClientID");
      %Active = %rs.getFieldValue("Active");
      %ClientVIP = %rs.getFieldValue("ClientVIP");
      if(%VIPFlag) 
      {
          for(%id = 0; %id < ClientGroup.getCount(); %id++)
          {
            %client = ClientGroup.getObject(%id);
            if(%client.ConnectedTime <= (getUnixTime() - 60) && !isObject(%client.Player) && %client != LiFxFullServerFixVIP.ConReq.client && !%ClientVIP)
            {
              %client.scheduleDelete("You have been ejected from the server due to inactivity (AFK) 1", 100);
              break;
            } 
            else if(%ClientID == %client.getCharacterId() && !%ClientVIP)
            {
              %client.scheduleDelete("You have been ejected from the server due to inactivity (AFK) 2", 100);
              break;
            }
          }
          if(ClientGroup.getCount() == %id) {
            %this.ConReq.Client.scheduleDelete("Server is full without idlers, try again in 5 mins", 100);
          }
      }
      else {    
        warn("Connection from" SPC %this.ConReq.NetAddress SPC "(" @ %this.ConReq.Name @ ")" SPC "dropped due to CR_SERVERFULL");
        %this.ConReq.Client.scheduleDelete("Server is full", 100);
      }
    }
    else {   
      warn("Connection from" SPC %this.ConReq.NetAddress SPC "(" @ %this.ConReq.Name @ ")" SPC "dropped due to CR_SERVERFULL");
      %this.ConReq.Client.scheduleDelete("Server is full", 100);
    }
    dbi.remove(%rs);
    %rs.delete();
  }
  function LiFxFullServerFixVIP::checkCharacterSelect(%this) {
    for(%id = 0; %id < ClientGroup.getCount(); %id++){if(!isObject(ClientGroup.getObject(%id).Player)){%client = ClientGroup.getObject(%id);%client.scheduleDelete("You have been kicked for being away from keyboard (AFK)", 100);}}

  }
};
activatePackage(LiFxFullServerFixVIP);
LiFx::registerCallback($LiFx::hooks::mods, setup, LiFxFullServerFixVIP);