import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminApiService } from '../../services/admin-api.service';

export interface UploadedImage {
  id: number;
  url: string;
  isMain?: boolean;
}

@Component({
  selector: 'app-admin-product-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl:'./product-form.html',
  styleUrl: './product-form.scss'
})
export class AdminProductForm implements OnInit {
  private readonly api = inject(AdminApiService);
  categories: any[] = []; brands: any[] = [];
  // Form fields matching image
  productName: string = '';
  category: string = '';
  brand: string = '';
  price: number | null = null;
  comparePrice: number | null = null;
  stockQuantity: number | null = null;
  sku: string = '';
  shortDescription: string = '';
  fullDescription: string = '';

  // Status & Featured
  status: 'Active' | 'Inactive' = 'Active';
  featuredProduct: boolean = true;

  // Additional Information
  tags: string = '';
  weight: string = '';
  length: string = '';
  width: string = '';
  height: string = '';

  // SEO Information
  metaTitle: string = '';
  metaDescription: string = '';

  // Product Images preview list
  images: UploadedImage[] = [
    {
      id: 1,
      url: '/images/wellness/neem-bottle-nature.jpg',
      isMain: true
    },
    {
      id: 2,
      url: '/images/wellness/neem-turmeric.jpg',
      isMain: false
    },
    {
      id: 3,
      url: '/images/wellness/patanjali-giloy.jpg',
      isMain: false
    }
  ];

  constructor(private router: Router) {}
  ngOnInit(): void { this.api.categories().subscribe(x=>this.categories=x); this.api.brands().subscribe(x=>this.brands=x); }

  removeImage(index: number, event: Event): void {
    event.stopPropagation();
    this.images.splice(index, 1);
    if (this.images.length > 0 && !this.images.some(img => img.isMain)) {
      this.images[0].isMain = true;
    }
  }

  setMainImage(index: number): void {
    this.images.forEach((img, i) => {
      img.isMain = i === index;
    });
  }

  triggerUpload(): void {
    const input = document.getElementById('imageFileInput') as HTMLInputElement;
    if (input) {
      input.click();
    }
  }

  onFilesSelected(event: any): void {
    const files: FileList = event.target.files;
    if (files && files.length > 0) {
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const reader = new FileReader();
        reader.onload = (e: any) => {
          this.images.push({
            id: Date.now() + i,
            url: e.target.result,
            isMain: this.images.length === 0
          });
        };
        reader.readAsDataURL(file);
      }
    }
  }

  saveProduct(): void {
    if (!this.productName.trim()) {
      alert('Please provide a Product Name');
      return;
    }
    const category = this.categories.find(x => String(x.id) === String(this.category) || x.name === this.category);
    const brand = this.brands.find(x => String(x.id) === String(this.brand) || x.name === this.brand);
    if (!category || !brand) { alert('Please select a valid category and brand.'); return; }
    this.api.createProduct({ name:this.productName, slug:this.productName.toLowerCase().replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,''), sku:this.sku, description:this.fullDescription, shortDescription:this.shortDescription, price:this.price || 0, originalPrice:this.comparePrice, stockQuantity:this.stockQuantity || 0, lowStockThreshold:5, imageUrl:this.images.find(x=>x.isMain)?.url || this.images[0]?.url, galleryJson:JSON.stringify(this.images.map(x=>x.url)), detailsJson:JSON.stringify({tags:this.tags,weight:this.weight,dimensions:{length:this.length,width:this.width,height:this.height}}), isFeatured:this.featuredProduct, isActive:this.status==='Active', categoryId:category.id, brandId:brand.id }).subscribe({ next:()=>{alert('Product saved successfully!');void this.router.navigate(['/admin/products']);}, error:e=>alert(e?.error?.title || 'Unable to save product.') });
  }
}
