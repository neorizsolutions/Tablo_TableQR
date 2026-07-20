import bruschetta from "@/assets/food-bruschetta.jpg";
import risotto from "@/assets/food-risotto.jpg";
import salmon from "@/assets/food-salmon.jpg";
import smoothie from "@/assets/food-smoothie.jpg";
import cake from "@/assets/food-cake.jpg";
import salad from "@/assets/food-salad.jpg";
import pizza from "@/assets/food-pizza.jpg";
import lemonade from "@/assets/food-lemonade.jpg";

const map: Record<string, string> = {
  "Tomato Basil Bruschetta": bruschetta,
  "Avocado Garden Bowl": salad,
  "Wild Mushroom Risotto": risotto,
  "Grilled Atlantic Salmon": salmon,
  "Margherita Wood-Fired Pizza": pizza,
  "Mint Lemonade": lemonade,
  "Garden Green Smoothie": smoothie,
  "Molten Chocolate Cake": cake,
};

export function imageFor(name: string): string {
  return map[name] ?? salad;
}
