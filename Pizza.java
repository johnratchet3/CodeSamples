import java.util.*;
import java.text.*;
/**
 *
 * @author John
 */
public class Pizza {
    private LinkedList<Ingredient> ingredients = new LinkedList<Ingredient>();
    private int sold = 0;
    
    
    public Pizza make(Kitchen kitchen){
        String searchWord; //below keeps looping until user says 'stop'.
        boolean validPizza = false;
        
        while(validPizza != true){
            while ((!".".equals(searchWord = readIngredient()))){
                ////I think I need to make a list here, split the input string into words, then input the words into the follow section one by one.
                
                boolean multiEntry;
                boolean usingCategory = false;
                if(searchWord.contains(",")){multiEntry = true;}
                else{multiEntry = false;}
                
                LinkedList<String> inputStringList = new LinkedList<String>();
                for (String searchComponent : searchWord.split(",")){
                    inputStringList.add(searchComponent);
                }
                
                //ok, lets contain all of the below into yet another for loop.
                //so, FOR EACH SEARCH COMPONENT, do the ol' ingredient adding process
                for (String inputString : inputStringList){
                    //so it seems like we could do the category selection here. hacky ofc, but it should work
                Category[] categories = kitchen.getCategory(); //make a new array of the list of categories
                for(Category category : categories){//for each category
                    //System.out.println("ok, we entered the category check loop");
                    //System.out.println(searchWord.toLowerCase() + category.getLName());
                    if(inputString.toLowerCase().equals(category.getLName())){//if the search matches the category name then;
                        System.out.println("Select from matches below: ");//print
                        int i = 1;
                        //make a linked list of the ingredients that match the category
                        LinkedList<Ingredient> matchTheIngredient = kitchen.getIngredients();
                        LinkedList<Ingredient> listOfMatchingIngredients = new LinkedList<Ingredient>();
                        for (Ingredient ingredient : matchTheIngredient){
                            if(category == ingredient.getCategory()){//if the category and the ingredient match
                                System.out.println(i + ". " + ingredient.getName() + " " + ingredient.getCategory().getName());
                                listOfMatchingIngredients.add(ingredient);
                                i++;
                            }
                        }
                        System.out.print("Selection: ");
                        i = In.nextInt();
                        for(Ingredient finalIngredient : listOfMatchingIngredients){
                            i--;
                            if(i==0){
                                ingredients.add(finalIngredient);
                                usingCategory = true;
                                System.out.println(toString());
                            }
                        }
                    }
                }
                    
                LinkedList<Ingredient> returnedIngredients = kitchen.matchingIngredients(inputString.toLowerCase());
                if(returnedIngredients.size()== 0 && usingCategory == false){
                    if(inputString.startsWith("-")){
                        remove(inputString.substring(1, inputString.length()));
                        if(multiEntry == false){System.out.println(toString());} //prints the current pizza's contents
                    }
                    else{
                        System.out.println("No ingredient matching " + inputString);
                    }
                }
                if(returnedIngredients.size() > 1 && usingCategory == false){//resolves partial matches
                    System.out.println("Select from matches below: ");
                    int i = 1;
                    for (Ingredient ingredient : returnedIngredients){
                        System.out.println(i + ". " + ingredient.toString());
                        i++;
                    }
                    System.out.print("Selection: ");
                    i = In.nextInt();
                    for (Ingredient ingredient : returnedIngredients){
                        i--;//this is a shitty loop used to select the part of the list that lines up with the user's number
                        if(i == 0){//from here on in, it's a copy of the code used in the 'single ingredient' entry. an improvment would be stopping the loop once below 0. but anyway...
                            if(duplicateTest(ingredient) == false){
                            if(withinMaxCheck(ingredient) == true){
                                this.ingredients.add(ingredient);
                                if(multiEntry == false){System.out.println(toString());} //prints the current pizza's contents
                            }

                            else{System.out.println("Can only add " + ingredient.getCategoryMax() + " " + ingredient.getCategory().getPName());}

                        }
                        else{System.out.println("Already added " + ingredient.getName() + " " + ingredient.getCategory().getName());}
                        }//end of ingredient adding code
                    }
                }//end of partial matches code
                if(returnedIngredients.size() == 1 && usingCategory == false){
                    for(Ingredient ingredient : returnedIngredients){
                        if(duplicateTest(ingredient) == false){
                            if(withinMaxCheck(ingredient) == true){
                                this.ingredients.add(ingredient);
                                if(multiEntry == false){System.out.println(toString());} //prints the current pizza's contents
                            }

                            else{System.out.println("Can only add " + ingredient.getCategoryMax() + " " + ingredient.getCategory().getPName());}

                        }
                        else{System.out.println("Already added " + ingredient.getName() + " " + ingredient.getCategory().getName());}
                    }
                }
                
                }
                if(multiEntry == true && usingCategory == false){System.out.println(toString());} //prints the current pizza's contents
                }
            validPizza = validityCheck(kitchen); //if the pizza is invalid, the process sysprints what's wrong, and keeps us in the readloop
        }
        return this; //if the pizza is invalid, the loops are done, and the completed pizza is returned
    }
    
    private boolean validityCheck(Kitchen kitchen){
        Category[] categories = kitchen.getCategory();
        boolean validity = true;
        for (int i = 0; i < categories.length; i++){//for each category...
            int min = categories[i].getMin();//get the minimum and...
            int matches = 0;//make a number of matches var, set to 0
            for(Ingredient ingredient : ingredients){//for all ingredients in the pizza...
                if(ingredient.getCategory() == categories[i]){//if they're the same type as the current category...
                    matches++;//add to matches
                }
            }
            if(matches < min){
                boolean hasMultiple;
                hasMultiple = categories[i].getMin() > 1;
                System.out.println("Must have at least " + min + " " + categories[i].getXName(hasMultiple));
                validity = false;
            }
        }
        //set the public variable 'validPizza' to t/f depending on satisfying category minimums.
        return validity;
}
    
    private boolean duplicateTest(Ingredient ingredient){
        for(Ingredient shiteIngredient : ingredients){
            if(ingredient == shiteIngredient){
                return true;
            }
        }
        return false;
    }
    
    private boolean withinMaxCheck(Ingredient ingredient){
        //get what category to search for
        Category category = ingredient.getCategory();
        //search current ingredient list for number of matches (of category)
        LinkedList<Ingredient> maxCheckIngredients = new LinkedList<Ingredient>();//make a list to store matches in
        int sameTypes = 0;
        for(Ingredient shiteIngredient : ingredients){
            if(shiteIngredient.getCategory() == category){
                sameTypes++; 
            }
        }
        //compare number of matches to max
        if(sameTypes == category.getMax()){return false;}
        else{return true;}
    }
    
    private void remove(String removeWord) {
        for(Ingredient ingredient : ingredients){
            if(ingredient.getLName().startsWith(removeWord)){
                ingredients.remove(ingredient);
                break;
            }
        }
    }
    
    Ingredient ingredient(Category category){
         for (Ingredient ingredient : ingredients){
             if(ingredient.getCategory() == category){
                 return ingredient;
             }
         }
         return null;
    }
    

    
    private String readIngredient(){
        System.out.print("Ingredient(s): ");
        return In.nextLine();
    }
            
    public double pizzaPrice(){
        double pizzaPrice = 0;
        for (Ingredient ingredient : ingredients){
            pizzaPrice += ingredient.getPrice();
        }
        return pizzaPrice;
    }
    
    private String crust(){
        for (Ingredient ingredient : ingredients){
            if(ingredient.getCategory() == Kitchen.CRUST){
                return (ingredient.toString());
            }
        }
        return "no crust";
    }
    
    private String sauce(){
        for (Ingredient ingredient : ingredients){
            if(ingredient.getCategory() == Kitchen.SAUCE){
                return (ingredient.toString());
            }
        }
        return "no sauce";
    }
    
    private String toppings(){
        LinkedList<Ingredient> matches = new LinkedList<Ingredient>();
        String toppingOutput = "";
        for (Ingredient ingredient : ingredients)
            if (ingredient.getCategory() == Kitchen.TOPPING){
                matches.add(ingredient);
            }
        for (Ingredient test : matches){
            toppingOutput += test.getName();
            toppingOutput += ", ";
        }
        
        if("".equals(toppingOutput)){return "no toppings";} //if no ingredients, return
        toppingOutput = toppingOutput.substring(0, ((toppingOutput.length())-2));
        //^I apologise for this abomination. takes off the last 2 chars of the output,
        //thus removing the ', ' from the end of the string.
        return toppingOutput;
    }
    
    public void sellPizza(){
        sold++;
    }
    
    public int getSold(){
        return sold;
    }
    
    public String formatted(double pizzaPrice){
        return new DecimalFormat("###,##0.00").format(pizzaPrice);
    }
    
     @Override
    public String toString(){
        return (crust() + " pizza with " + toppings() + " and " + sauce() + ": $" + formatted(pizzaPrice()));
    }

    public LinkedList<Ingredient> getIngredients() {
        return ingredients;
    }

    
}

