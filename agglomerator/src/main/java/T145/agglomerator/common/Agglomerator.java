package T145.agglomerator.common;

import java.io.File;
import java.lang.annotation.Annotation;
import java.util.HashMap;
import java.util.logging.Level;
import java.util.logging.Logger;

import net.minecraft.block.Block;
import net.minecraft.enchantment.Enchantment;
import net.minecraft.item.Item;
import net.minecraft.item.ItemBlock;
import net.minecraftforge.common.Configuration;
import net.minecraftforge.common.Property;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.Mod.EventHandler;
import cpw.mods.fml.common.event.FMLPreInitializationEvent;
import cpw.mods.fml.common.network.NetworkMod;

@Mod(modid = Agglomerator.modid, name = Agglomerator.modid, version = Agglomerator.version)
@NetworkMod(clientSideRequired = true, serverSideRequired = true)
public class Agglomerator {
	public static final String modid = "Agglomerator", version = "1.1";
	public static String[] blockList = new String[4096], itemList = new String[32000], enchantmentList = new String[256];

	public static HashMap<String, Integer> blockMapping  = new HashMap<String, Integer>(), itemMapping  = new HashMap<String, Integer>(), enchantmentMapping  = new HashMap<String, Integer>();
	public static boolean isBlockItem;

	public static Configuration config;
	public static Logger logger;

	@EventHandler
	public void preInit(FMLPreInitializationEvent e) {
		logger = e.getModLog();
	}

	public static String getModNamespace() {
		String res = null;
		isBlockItem = false;
		try {
			throw new Exception();
		} catch (Exception ex) {
			final StackTraceElement[] stack = ex.getStackTrace();
			for (final StackTraceElement e : ex.getStackTrace()) {
				Class<?> cl = null;
				label: {
					try {
						cl = Class.forName(e.getClassName());
					} catch (ClassNotFoundException ee) {
						break label;
					}
					if (ItemBlock.class.isAssignableFrom(cl))
						isBlockItem = true;
					if (!cl.getName().startsWith("sun.")) {
						if (!cl.getName().startsWith("cpw.mods.fml.")) {
							if (!cl.getName().startsWith("java.")) {
								if (!Block.class.isAssignableFrom(cl)) {
									if (!Item.class.isAssignableFrom(cl)) {
										if (!Enchantment.class.isAssignableFrom(cl)) {
											if (!Agglomerator.class.isAssignableFrom(cl)) {
												if (res == null)
													res = e.getClassName();
												if (Mod.class.isAssignableFrom(cl)) {
													res = e.getClassName();
													break;
												}
												if (cl.isAnnotationPresent((Class<? extends Annotation>) Mod.class)) {
													res = e.getClassName();
													break;
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			return res;
		}
	}

	// find a way to fully integrate this into the preInit event
	static void loadConfig() {
		if (config != null)
			return;
		final Configuration cfg = new Configuration(new File("agglomerator.txt"));
		cfg.load();
		config = cfg;
		for (int i = 0; i < blockList.length; ++i) {
			final Property p = getBlockId(i, "");
			if (p != null) {
				blockList[i] = p.getString();
				blockMapping.put(blockList[i], i);
			}
		}
		for (int i = 0; i < itemList.length; ++i) {
			final Property p = getItemId(i, "");
			if (p != null) {
				itemList[i + 256] = p.getString();
				itemMapping.put(itemList[i + 256], i);
			}
		}
		for (int i = 0; i < enchantmentList.length; ++i) {
			final Property p = getEnchantmentId(i, "");
			if (p != null) {
				enchantmentList[i] = p.getString();
				enchantmentMapping.put(enchantmentList[i], i);
			}
		}
	}

	static Property getBlockId(final int id, final String def) {
		return getId("blocks", "%04d", id, def);
	}

	static Property getItemId(final int id, final String def) {
		return getId("items", "%05d", id, def);
	}

	static Property getEnchantmentId(final int id, final String def) {
		return getId("enchantments", "%03d", id, def);
	}

	static Property getId(final String category, final String keyMask, final int id, final String def) {
		final String key = String.format(keyMask, id);
		return !config.hasKey(category, key) && def.isEmpty() ? null : config.get(category, key, def);
	}

	public static Property configGetBlockInternal(final Configuration conf, final String category, final String blockName, int blockId, final String comment, final int lower, final int upper) {
		loadConfig();
		final String key = getModNamespace() + "|" + blockName + "|" + blockId;
		final Property prop = conf.get(category, blockName, -1, comment);
		final int propId = prop.getInt();

		if (propId != -1)
			blockId = propId;
		else if (blockMapping.containsKey(key))
			blockId = blockMapping.get(key);
		if (blockId >= blockList.length)
			blockId = upper - 1;
		if (blockId < lower) {
			logger.log(Level.INFO, "Rejecting ID " + lower + " for block " + key);
			blockId = upper - 1;
		}

		for (int possibleId = upper; possibleId >= lower; --possibleId) {
			final int id = (possibleId == upper) ? blockId : possibleId;
			if (Block.blocksList[id] == null) {
				if (itemList[id] == null) {
					final boolean empty = blockList[id] == null;
					final boolean fits = !empty && blockList[id].equals(key);
					if (empty || fits) {
						if (empty && config != null) {
							blockList[id] = key;
							blockMapping.put(key, id);
							getBlockId(id, key).set(key);
							config.save();
						}
						prop.set(id);
						if (id != blockId) {
							String line = "Changing block ID for " + key + " because of conflict ";
							if (itemList[blockId] != null) {
								line = line + "with item " + itemList[blockId];
							} else if (blockList[blockId] != null) {
								line = line + "with block " + blockList[blockId];
							} else if (Item.itemsList[blockId] != null) {
								line = line + "with item " + Item.itemsList[blockId];
							} else if (Block.blocksList[blockId] != null) {
								line = line + "with block " + Block.blocksList[blockId];
							} else
								line += " (likely a bug)";
							line = line + "; from " + blockId + " to " + id;
							logger.log(Level.INFO, line);
						}
						return prop;
					}
				}
			}
		}
		throw new RuntimeException("Agglomerator: No more block IDs available for " + key + "!");
	}

	public static Property configGetItem(final Configuration conf, final String category, final String name, int itemId, final String comment) {
		loadConfig();
		final String key = getModNamespace() + "|" + name + "|" + itemId;
		final Property prop = conf.get(category, name, -1, comment);
		final int propId = prop.getInt();

		if (propId != -1) {
			itemId = propId;
		} else if (itemMapping.containsKey(key)) {
			itemId = itemMapping.get(key);
		}

		final int lower = 256, upper = Item.itemsList.length - 256;
		if (itemId < 0 || itemId + 256 >= itemList.length)
			itemId = upper - 1;
		final int shiftedItemId = itemId + 256;
		for (int possibleId = upper; possibleId >= lower; --possibleId) {
			final int id = (possibleId == upper) ? itemId : possibleId;
			final int shiftedId = id + 256;
			if (Item.itemsList[shiftedId] == null) {
				if (shiftedId <= 0 || shiftedId >= blockList.length || blockList[shiftedId] == null) {
					final boolean empty = itemList[shiftedId] == null;
					final boolean fits = !empty && itemList[shiftedId].equals(key);
					if (empty || fits) {
						if (empty && config != null) {
							itemList[shiftedId] = key;
							itemMapping.put(key, id);
							getItemId(id, key).set(key);
							config.save();
						}
						prop.set(id);
						if (id != itemId) {
							String line = "Changing item ID for " + key + " because of conflict ";
							if (shiftedItemId > 0 && shiftedItemId < blockList.length && blockList[shiftedItemId] != null) {
								line = line + "with block " + blockList[shiftedItemId];
							} else if (itemList[shiftedItemId] != null) {
								line = line + "with block " + itemList[shiftedItemId];
							} else if (shiftedItemId > 0 && shiftedItemId < blockList.length && Block.blocksList[shiftedItemId] != null) {
								line = line + "with block " + Block.blocksList[shiftedItemId];
							} else if (Item.itemsList[shiftedItemId] != null) {
								line = line + "with item " + Item.itemsList[shiftedItemId];
							} else
								line += " (likely a bug)";
							line = line + "; from " + itemId + " to " + id;
							logger.log(Level.INFO, line);
						}
						return prop;
					}
				}
			}
		}
		throw new RuntimeException("Agglomerator: No more item IDs available for " + key + "!");
	}

	public static int configGet(final Configuration conf, final String category, final String name, final int id) {
		if (category.equals("block")) {
			if (id == -1)
				return id;
			final Property prop = configGetBlockInternal(conf, category, name, id, "", 0, Block.blocksList.length);
			return prop.getInt(id);
		} else {
			if (!category.equals("item") || id == -1)
				return id;
			final Property prop = configGetItem(conf, category, name, id, "");
			return prop.getInt(id);
		}
	}

	public static int transformEnchantmentId(int enchantmentId) {
		if (Enchantment.infinity == null)
			return enchantmentId;
		loadConfig();

		final String key = getModNamespace() + "|" + enchantmentId;
		if (enchantmentMapping.containsKey(key))
			enchantmentId = enchantmentMapping.get(key);
		final int lower = 0, upper = 256;
		if (enchantmentId < 0 || enchantmentId >= enchantmentList.length)
			enchantmentId = upper - 1;

		for (int possibleId = upper; possibleId >= lower; --possibleId) {
			final int id = (possibleId == upper) ? enchantmentId : possibleId;
			if (Enchantment.enchantmentsList[id] == null) {
				final boolean empty = enchantmentList[id] == null;
				final boolean fits = !empty && enchantmentList[id].equals(key);
				if (empty || fits) {
					if (empty && config != null) {
						enchantmentList[id] = key;
						enchantmentMapping.put(key, id);
						getEnchantmentId(id, key).set(key);
						config.save();
					}
					if (id != enchantmentId)
						logger.log(Level.INFO, "Changing enchantment ID for " + key + " from " + enchantmentId + " to " + id + " because of a conflict with " + enchantmentList[enchantmentId]);
					return id;
				}
			}
		}
		return enchantmentId;
	}
}