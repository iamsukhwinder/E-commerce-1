import { Injectable, signal, inject } from '@angular/core';
import { ApiClient } from '../api/api-client.service';

export interface ExploreCategory {
  id: string;
  title: string;
  subtitle: string;
  image: string;
  bgGradient: string;
  arrowBg: string;
  arrowColor: string;
  link?: string;
  categoryQuery?: string;
}

const STORAGE_EXPLORE_KEY = 'herbalmart_explore_categories_v1';

@Injectable({
  providedIn: 'root',
})
export class ExploreCategoryService {
  private readonly api = inject(ApiClient);
  private readonly defaultCategories: ExploreCategory[] = [
    {
      id: 'home-living',
      title: 'Home & Living',
      subtitle: 'Comfort for every space',
      image: '/images/explore-category/image 38.png',
      bgGradient: 'linear-gradient(180deg, #fdf6ea 0%, #fbf0dc 100%)',
      arrowBg: '#faeed7',
      arrowColor: '#b45309',
      categoryQuery: 'craft-living',
    },
    {
      id: 'beauty',
      title: 'Beauty',
      subtitle: 'Skincare, makeup & more',
      image: '/images/explore-category/image 39.png',
      bgGradient: 'linear-gradient(180deg, #fcf0f5 0%, #fae2ed 100%)',
      arrowBg: '#fce7f3',
      arrowColor: '#be185d',
      categoryQuery: 'beauty',
    },
    {
      id: 'fashion',
      title: 'Fashion',
      subtitle: 'Style for every you',
      image: '/images/explore-category/image 40.png',
      bgGradient: 'linear-gradient(180deg, #edf2fe 0%, #e1e9fd 100%)',
      arrowBg: '#e0e7ff',
      arrowColor: '#4338ca',
      categoryQuery: 'fashion',
    },
    {
      id: 'food-pantry',
      title: 'Food & Pantry',
      subtitle: 'Global tastes, local comfort',
      image: '/images/explore-category/image 41.png',
      bgGradient: 'linear-gradient(180deg, #eefaf1 0%, #e1f5e6 100%)',
      arrowBg: '#dcfce7',
      arrowColor: '#15803d',
      categoryQuery: 'pantry',
    },
    {
      id: 'ayurveda',
      title: 'Ayurveda',
      subtitle: 'Traditional wellness',
      image: '/images/explore-category/image 42.png',
      bgGradient: 'linear-gradient(180deg, #eef7ed 0%, #e2f2e0 100%)',
      arrowBg: '#dcfce7',
      arrowColor: '#166534',
      categoryQuery: 'wellness',
    },
    {
      id: 'gifts',
      title: 'Gifts',
      subtitle: 'Thoughtful finds',
      image: '/images/explore-category/image 43.png',
      bgGradient: 'linear-gradient(180deg, #f3f0fd 0%, #e9e3fc 100%)',
      arrowBg: '#ede9fe',
      arrowColor: '#6d28d9',
      categoryQuery: 'wellness',
    },
  ];

  readonly categories = signal<ExploreCategory[]>([]);

  constructor() {
    this.initCategories();
  }

  private initCategories(): void {
    this.categories.set(JSON.parse(JSON.stringify(this.defaultCategories)));
    this.api.get<any[]>('/categories').subscribe({ next: rows => this.categories.set(rows.map((x, i) => ({ id:String(x.id),title:x.name,subtitle:'Explore authentic products',image:x.imageUrl||this.defaultCategories[i%this.defaultCategories.length].image,bgGradient:this.defaultCategories[i%this.defaultCategories.length].bgGradient,arrowBg:this.defaultCategories[i%this.defaultCategories.length].arrowBg,arrowColor:this.defaultCategories[i%this.defaultCategories.length].arrowColor,categoryQuery:x.slug }))), error:()=>{} });
  }

  private persist(): void {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        localStorage.setItem(STORAGE_EXPLORE_KEY, JSON.stringify(this.categories()));
      } catch (e) {
        console.warn('Could not persist explore categories to localStorage', e);
      }
    }
  }

  getCategoryById(id: string): ExploreCategory | undefined {
    return this.categories().find((c) => c.id === id);
  }

  addCategory(data: Partial<ExploreCategory>): ExploreCategory {
    const list = this.categories();
    const newId = data.id || ('cat-' + Date.now());
    const newCat: ExploreCategory = {
      id: newId,
      title: data.title || 'New Category',
      subtitle: data.subtitle || 'Everyday essentials',
      image: data.image || '/images/explore-category/image 38.png',
      bgGradient: data.bgGradient || 'linear-gradient(180deg, #fdf6ea 0%, #fbf0dc 100%)',
      arrowBg: data.arrowBg || '#faeed7',
      arrowColor: data.arrowColor || '#b45309',
      categoryQuery: data.categoryQuery || 'wellness',
      link: data.link,
    };

    const updated = [...list, newCat];
    this.categories.set(updated);
    this.api.post<any>('/admin/categories', { id:0,name:newCat.title,slug:newCat.categoryQuery||newCat.id,imageUrl:newCat.image,parentId:null,isActive:true,displayOrder:updated.length }).subscribe({next:x=>{newCat.id=String(x.id);}});
    return newCat;
  }

  updateCategory(id: string, patch: Partial<ExploreCategory>): boolean {
    const list = this.categories();
    const idx = list.findIndex((c) => c.id === id);
    if (idx === -1) return false;

    const updated = [...list];
    updated[idx] = {
      ...updated[idx],
      ...patch,
    };
    this.categories.set(updated);
    if (/^\d+$/.test(id)) this.api.put(`/admin/categories/${id}`, { id:Number(id),name:updated[idx].title,slug:updated[idx].categoryQuery||id,imageUrl:updated[idx].image,parentId:null,isActive:true,displayOrder:idx+1 }).subscribe();
    return true;
  }

  replaceImage(id: string, newImage: string): boolean {
    return this.updateCategory(id, { image: newImage });
  }

  deleteCategory(id: string): boolean {
    const list = this.categories().filter((c) => c.id !== id);
    if (list.length === this.categories().length) return false;

    this.categories.set(list);
    if (/^\d+$/.test(id)) this.api.delete(`/admin/categories/${id}`).subscribe();
    return true;
  }

  resetToDefaults(): void {
    this.categories.set(JSON.parse(JSON.stringify(this.defaultCategories)));
    this.persist();
  }
}

